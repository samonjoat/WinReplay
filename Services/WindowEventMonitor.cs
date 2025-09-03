using WinRelay.Native;
using WinRelay.Models;

namespace WinRelay.Services
{
    /// <summary>
    /// Monitors window events using Win32 SetWinEventHook
    /// </summary>
    public class WindowEventMonitor : IDisposable
    {
        private Win32Api.WinEventDelegate? _windowEventDelegate;
        private IntPtr _hookHandle = IntPtr.Zero;
        private readonly object _hookLock = new();
        private bool _isMonitoring = false;
        private bool _disposed = false;

        // Fallback polling timer
        private System.Timers.Timer? _pollingTimer;
        private readonly HashSet<IntPtr> _knownWindows = new();
        private readonly object _windowsLock = new();

        public event EventHandler<WindowEventArgs>? WindowCreated;
        public event EventHandler<WindowEventArgs>? WindowDestroyed;
        public event EventHandler<WindowEventArgs>? WindowShown;
        public event EventHandler<WindowEventArgs>? WindowHidden;

        public bool IsMonitoring => _isMonitoring;
        public bool UsePollingFallback { get; set; } = false;
        public int PollingIntervalMs { get; set; } = 500;

        /// <summary>
        /// Starts monitoring window events
        /// </summary>
        public void StartMonitoring()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(WindowEventMonitor));

            lock (_hookLock)
            {
                if (_isMonitoring) return;

                try
                {
                    if (UsePollingFallback)
                    {
                        StartPollingMode();
                    }
                    else
                    {
                        if (StartHookMode())
                        {
                            _isMonitoring = true;
                        }
                        else
                        {
                            // Fallback to polling if hook fails
                            System.Diagnostics.Debug.WriteLine("Hook mode failed, falling back to polling");
                            StartPollingMode();
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to start window monitoring: {ex.Message}");
                    // Try polling as last resort
                    if (!UsePollingFallback)
                    {
                        StartPollingMode();
                    }
                }
            }
        }

        /// <summary>
        /// Stops monitoring window events
        /// </summary>
        public void StopMonitoring()
        {
            lock (_hookLock)
            {
                if (!_isMonitoring) return;

                try
                {
                    // Stop hook if active
                    if (_hookHandle != IntPtr.Zero)
                    {
                        Win32Api.UnhookWinEvent(_hookHandle);
                        _hookHandle = IntPtr.Zero;
                    }

                    // Stop polling timer
                    _pollingTimer?.Stop();
                    _pollingTimer?.Dispose();
                    _pollingTimer = null;

                    _isMonitoring = false;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error stopping window monitoring: {ex.Message}");
                }
            }
        }

        private bool StartHookMode()
        {
            try
            {
                _windowEventDelegate = new Win32Api.WinEventDelegate(WindowEventCallback);
                
                _hookHandle = Win32Api.SetWinEventHook(
                    Win32Api.EVENT_OBJECT_CREATE,
                    Win32Api.EVENT_OBJECT_HIDE,
                    IntPtr.Zero,
                    _windowEventDelegate,
                    0, 0,
                    Win32Api.WINEVENT_OUTOFCONTEXT | Win32Api.WINEVENT_SKIPOWNPROCESS);

                if (_hookHandle != IntPtr.Zero)
                {
                    _isMonitoring = true;
                    System.Diagnostics.Debug.WriteLine("Window event hook installed successfully");
                    return true;
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Failed to install window event hook");
                    return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting up window event hook: {ex.Message}");
                return false;
            }
        }

        private void StartPollingMode()
        {
            try
            {
                // Initialize known windows
                lock (_windowsLock)
                {
                    _knownWindows.Clear();
                    EnumerateExistingWindows();
                }

                _pollingTimer = new System.Timers.Timer(PollingIntervalMs)
                {
                    AutoReset = true,
                    Enabled = true
                };
                _pollingTimer.Elapsed += OnPollingTimerElapsed;

                _isMonitoring = true;
                System.Diagnostics.Debug.WriteLine($"Window polling started with {PollingIntervalMs}ms interval");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting polling mode: {ex.Message}");
            }
        }

        private void WindowEventCallback(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, 
            int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            try
            {
                // Only process events for windows (not child objects)
                if (idObject != 0 || idChild != 0) return;
                if (hwnd == IntPtr.Zero) return;

                var eventArgs = new WindowEventArgs(hwnd, eventType);

                switch (eventType)
                {
                    case Win32Api.EVENT_OBJECT_CREATE:
                        // Add delay to ensure window is fully created
                        Task.Delay(100).ContinueWith(_ => 
                        {
                            if (Win32Api.IsWindow(hwnd) && Win32Api.IsNormalWindow(hwnd))
                            {
                                WindowCreated?.Invoke(this, eventArgs);
                            }
                        });
                        break;

                    case Win32Api.EVENT_OBJECT_DESTROY:
                        WindowDestroyed?.Invoke(this, eventArgs);
                        break;

                    case Win32Api.EVENT_OBJECT_SHOW:
                        if (Win32Api.IsNormalWindow(hwnd))
                        {
                            WindowShown?.Invoke(this, eventArgs);
                        }
                        break;

                    case Win32Api.EVENT_OBJECT_HIDE:
                        WindowHidden?.Invoke(this, eventArgs);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in window event callback: {ex.Message}");
            }
        }

        private void OnPollingTimerElapsed(object? sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                var currentWindows = new HashSet<IntPtr>();
                
                // Enumerate all current windows
                Win32Api.EnumWindows((hWnd, lParam) =>
                {
                    if (Win32Api.IsNormalWindow(hWnd))
                    {
                        currentWindows.Add(hWnd);
                    }
                    return true;
                }, IntPtr.Zero);

                lock (_windowsLock)
                {
                    // Find new windows
                    var newWindows = currentWindows.Except(_knownWindows).ToList();
                    foreach (var newWindow in newWindows)
                    {
                        var eventArgs = new WindowEventArgs(newWindow, Win32Api.EVENT_OBJECT_CREATE);
                        WindowCreated?.Invoke(this, eventArgs);
                    }

                    // Find removed windows
                    var removedWindows = _knownWindows.Except(currentWindows).ToList();
                    foreach (var removedWindow in removedWindows)
                    {
                        var eventArgs = new WindowEventArgs(removedWindow, Win32Api.EVENT_OBJECT_DESTROY);
                        WindowDestroyed?.Invoke(this, eventArgs);
                    }

                    // Update known windows
                    _knownWindows.Clear();
                    foreach (var window in currentWindows)
                    {
                        _knownWindows.Add(window);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in polling timer: {ex.Message}");
            }
        }

        private void EnumerateExistingWindows()
        {
            Win32Api.EnumWindows((hWnd, lParam) =>
            {
                if (Win32Api.IsNormalWindow(hWnd))
                {
                    _knownWindows.Add(hWnd);
                }
                return true;
            }, IntPtr.Zero);
        }

        /// <summary>
        /// Forces a check for new windows (useful for testing)
        /// </summary>
        public void ForceWindowCheck()
        {
            if (UsePollingFallback)
            {
                OnPollingTimerElapsed(null, new System.Timers.ElapsedEventArgs(DateTime.Now));
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            StopMonitoring();
            _disposed = true;
            GC.SuppressFinalize(this);
        }

        ~WindowEventMonitor()
        {
            if (!_disposed)
            {
                StopMonitoring();
            }
        }
    }

    /// <summary>
    /// Event arguments for window events
    /// </summary>
    public class WindowEventArgs : EventArgs
    {
        public IntPtr WindowHandle { get; }
        public uint EventType { get; }
        public WindowInfo? WindowInfo { get; private set; }
        public DateTime Timestamp { get; }

        public WindowEventArgs(IntPtr windowHandle, uint eventType)
        {
            WindowHandle = windowHandle;
            EventType = eventType;
            Timestamp = DateTime.Now;

            // Load window info if window still exists
            if (Win32Api.IsWindow(windowHandle))
            {
                try
                {
                    WindowInfo = new WindowInfo(windowHandle);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load window info: {ex.Message}");
                }
            }
        }

        public string EventTypeName
        {
            get
            {
                return EventType switch
                {
                    Win32Api.EVENT_OBJECT_CREATE => "Created",
                    Win32Api.EVENT_OBJECT_DESTROY => "Destroyed",
                    Win32Api.EVENT_OBJECT_SHOW => "Shown",
                    Win32Api.EVENT_OBJECT_HIDE => "Hidden",
                    _ => "Unknown"
                };
            }
        }

        public override string ToString()
        {
            return $"Window {EventTypeName}: {WindowInfo?.Title ?? "Unknown"} (Handle: {WindowHandle})";
        }
    }
}