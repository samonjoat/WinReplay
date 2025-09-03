using System.Text.RegularExpressions;
using WinRelay.Native;
using WinRelay.Models;

namespace WinRelay.Services
{
    /// <summary>
    /// Core window management service that handles window centering and positioning
    /// </summary>
    public class WindowManager : IDisposable
    {
        private readonly SettingsManager _settingsManager;
        private readonly WindowEventMonitor _eventMonitor;
        private readonly SizeCalculator _sizeCalculator;
        private readonly MonitorManager _monitorManager;
        
        private readonly object _processingLock = new();
        private readonly HashSet<IntPtr> _processedWindows = new();
        private bool _disposed = false;
        private bool _isEnabled = true;

        public event EventHandler<WindowProcessedEventArgs>? WindowProcessed;
        public event EventHandler<WindowExcludedEventArgs>? WindowExcluded;

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                _isEnabled = value;
                if (value && !_eventMonitor.IsMonitoring)
                {
                    StartMonitoring();
                }
                else if (!value && _eventMonitor.IsMonitoring)
                {
                    StopMonitoring();
                }
            }
        }

        public WindowManager(SettingsManager settingsManager)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _eventMonitor = new WindowEventMonitor();
            _sizeCalculator = new SizeCalculator();
            _monitorManager = new MonitorManager();

            // Subscribe to events
            _eventMonitor.WindowCreated += OnWindowCreated;
            _eventMonitor.WindowShown += OnWindowShown;
            _settingsManager.SettingsChanged += OnSettingsChanged;

            // Configure event monitor based on settings
            UpdateEventMonitorSettings();
        }

        /// <summary>
        /// Starts monitoring window events
        /// </summary>
        public void StartMonitoring()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(WindowManager));
            
            try
            {
                if (_isEnabled && !_eventMonitor.IsMonitoring)
                {
                    _eventMonitor.StartMonitoring();
                    System.Diagnostics.Debug.WriteLine("Window manager monitoring started");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to start window monitoring: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops monitoring window events
        /// </summary>
        public void StopMonitoring()
        {
            try
            {
                if (_eventMonitor.IsMonitoring)
                {
                    _eventMonitor.StopMonitoring();
                    System.Diagnostics.Debug.WriteLine("Window manager monitoring stopped");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping window monitoring: {ex.Message}");
            }
        }

        /// <summary>
        /// Centers the currently active window
        /// </summary>
        public bool CenterActiveWindow()
        {
            try
            {
                IntPtr activeWindow = Win32Api.GetForegroundWindow();
                if (activeWindow == IntPtr.Zero) return false;

                return CenterWindow(activeWindow);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error centering active window: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Centers the specified window
        /// </summary>
        public bool CenterWindow(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero) return false;

            try
            {
                var windowInfo = new WindowInfo(windowHandle);
                return ProcessWindow(windowInfo, forceProcess: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error centering window: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Moves a window to the next monitor
        /// </summary>
        public bool MoveWindowToNextMonitor(IntPtr windowHandle)
        {
            try
            {
                var currentMonitor = _monitorManager.GetMonitorFromWindow(windowHandle);
                if (currentMonitor == null) return false;

                var nextMonitor = _monitorManager.GetNextMonitor(currentMonitor);
                if (nextMonitor == null || nextMonitor.Handle == currentMonitor.Handle) return false;

                return _monitorManager.MoveWindowToMonitor(windowHandle, nextMonitor, centerWindow: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error moving window to next monitor: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Moves a window to the previous monitor
        /// </summary>
        public bool MoveWindowToPreviousMonitor(IntPtr windowHandle)
        {
            try
            {
                var currentMonitor = _monitorManager.GetMonitorFromWindow(windowHandle);
                if (currentMonitor == null) return false;

                var prevMonitor = _monitorManager.GetPreviousMonitor(currentMonitor);
                if (prevMonitor == null || prevMonitor.Handle == currentMonitor.Handle) return false;

                return _monitorManager.MoveWindowToMonitor(windowHandle, prevMonitor, centerWindow: true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error moving window to previous monitor: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Resizes a window to specified preset
        /// </summary>
        public bool ResizeWindowToPreset(IntPtr windowHandle, WindowPreset preset)
        {
            try
            {
                var monitor = _monitorManager.GetMonitorFromWindow(windowHandle);
                if (monitor == null) return false;

                var targetSize = _sizeCalculator.CalculatePresetSize(monitor, preset);
                var centerPos = _sizeCalculator.CalculateCenterPosition(monitor, targetSize);

                return Win32Api.SetWindowPos(windowHandle, IntPtr.Zero,
                    centerPos.X, centerPos.Y, targetSize.Width, targetSize.Height,
                    Win32Api.SWP_NOZORDER);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error resizing window to preset: {ex.Message}");
                return false;
            }
        }

        private void OnWindowCreated(object? sender, WindowEventArgs e)
        {
            if (!_isEnabled || e.WindowHandle == IntPtr.Zero) return;

            // Add delay to ensure window is fully initialized
            Task.Delay(_settingsManager.Settings.WindowCentering.ProcessingDelayMs)
                .ContinueWith(_ =>
                {
                    try
                    {
                        if (Win32Api.IsWindow(e.WindowHandle) && e.WindowInfo != null)
                        {
                            ProcessWindow(e.WindowInfo);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error in delayed window processing: {ex.Message}");
                    }
                });
        }

        private void OnWindowShown(object? sender, WindowEventArgs e)
        {
            // Handle windows that become visible (might need centering)
            if (!_isEnabled || e.WindowHandle == IntPtr.Zero) return;

            var config = _settingsManager.Settings.WindowCentering;
            if (config.Mode == CenteringMode.Automatic && e.WindowInfo != null)
            {
                ProcessWindow(e.WindowInfo);
            }
        }

        private bool ProcessWindow(WindowInfo windowInfo, bool forceProcess = false)
        {
            if (!_isEnabled || windowInfo.Handle == IntPtr.Zero) return false;

            lock (_processingLock)
            {
                // Skip if already processed (unless forced)
                if (!forceProcess && _processedWindows.Contains(windowInfo.Handle)) return false;

                try
                {
                    var config = _settingsManager.Settings.WindowCentering;

                    // Check if processing is enabled
                    if (!forceProcess && config.Mode == CenteringMode.Disabled) return false;

                    // Update window info
                    windowInfo.UpdateFromHandle();

                    // Check exclusion rules
                    if (ShouldExcludeWindow(windowInfo, config))
                    {
                        WindowExcluded?.Invoke(this, new WindowExcludedEventArgs(windowInfo, "Matched exclusion rule"));
                        return false;
                    }

                    // Check window state constraints
                    if (!forceProcess && !ShouldProcessWindowState(windowInfo, config))
                    {
                        WindowExcluded?.Invoke(this, new WindowExcludedEventArgs(windowInfo, "Window state excluded"));
                        return false;
                    }

                    // Get target monitor
                    var targetMonitor = _monitorManager.GetMonitorFromWindow(windowInfo.Handle);
                    if (targetMonitor == null)
                    {
                        WindowExcluded?.Invoke(this, new WindowExcludedEventArgs(windowInfo, "No target monitor found"));
                        return false;
                    }

                    // Calculate target position and size
                    var windowTarget = _sizeCalculator.CalculateWindowTarget(windowInfo, targetMonitor, config);
                    
                    // Validate target
                    if (!windowTarget.IsValidTarget())
                    {
                        WindowExcluded?.Invoke(this, new WindowExcludedEventArgs(windowInfo, "Invalid target dimensions"));
                        return false;
                    }

                    // Apply window position and size
                    bool success = ApplyWindowTarget(windowInfo.Handle, windowTarget, config.ForceResize);

                    if (success)
                    {
                        _processedWindows.Add(windowInfo.Handle);
                        WindowProcessed?.Invoke(this, new WindowProcessedEventArgs(windowInfo, windowTarget));
                    }

                    return success;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error processing window: {ex.Message}");
                    return false;
                }
            }
        }

        private bool ShouldExcludeWindow(WindowInfo windowInfo, WindowCenteringConfig config)
        {
            foreach (var rule in config.ExclusionRules.Where(r => r.Enabled))
            {
                try
                {
                    bool matches = rule.Type switch
                    {
                        ExclusionRuleType.ProcessName => MatchesPattern(windowInfo.ProcessName, rule),
                        ExclusionRuleType.WindowTitle => MatchesPattern(windowInfo.Title, rule),
                        ExclusionRuleType.WindowClass => MatchesPattern(windowInfo.ClassName, rule),
                        _ => false
                    };

                    if (matches) return true;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error checking exclusion rule: {ex.Message}");
                }
            }

            return false;
        }

        private bool MatchesPattern(string text, ExclusionRule rule)
        {
            if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(rule.Pattern))
                return false;

            try
            {
                if (rule.IsRegex)
                {
                    return Regex.IsMatch(text, rule.Pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
                }
                else
                {
                    return text.IndexOf(rule.Pattern, StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error matching pattern '{rule.Pattern}': {ex.Message}");
                return false;
            }
        }

        private bool ShouldProcessWindowState(WindowInfo windowInfo, WindowCenteringConfig config)
        {
            // Skip maximized windows if configured
            if (config.SkipMaximizedWindows && windowInfo.IsMaximized) return false;

            // Skip minimized windows if configured
            if (config.SkipMinimizedWindows && windowInfo.IsMinimized) return false;

            // Only process visible windows
            if (!windowInfo.IsVisible) return false;

            // Must be a normal window
            if (!windowInfo.IsNormalWindow()) return false;

            return true;
        }

        private bool ApplyWindowTarget(IntPtr windowHandle, WindowTarget target, bool forceResize)
        {
            try
            {
                uint flags = Win32Api.SWP_NOZORDER;

                // Check if we should force resize or if window is resizable
                if (!forceResize)
                {
                    // Check if window is resizable
                    var style = Win32Api.GetWindowLong(windowHandle, -16); // GWL_STYLE
                    const long WS_THICKFRAME = 0x00040000L;
                    const long WS_MAXIMIZEBOX = 0x00010000L;
                    
                    bool isResizable = (style & (WS_THICKFRAME | WS_MAXIMIZEBOX)) != 0;
                    if (!isResizable)
                    {
                        flags |= Win32Api.SWP_NOSIZE; // Don't resize non-resizable windows
                    }
                }

                return Win32Api.SetWindowPos(windowHandle, IntPtr.Zero,
                    target.Bounds.X, target.Bounds.Y,
                    target.Bounds.Width, target.Bounds.Height,
                    flags);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying window target: {ex.Message}");
                return false;
            }
        }

        private void OnSettingsChanged(object? sender, ApplicationSettings settings)
        {
            UpdateEventMonitorSettings();
        }

        private void UpdateEventMonitorSettings()
        {
            var startupConfig = _settingsManager.Settings.Startup;
            _eventMonitor.UsePollingFallback = startupConfig.UsePollingFallback;
            _eventMonitor.PollingIntervalMs = startupConfig.PollingIntervalMs;
        }

        /// <summary>
        /// Clears the processed windows cache
        /// </summary>
        public void ClearProcessedWindowsCache()
        {
            lock (_processingLock)
            {
                _processedWindows.Clear();
            }
        }

        /// <summary>
        /// Gets debug information about the window manager
        /// </summary>
        public string GetDebugInfo()
        {
            lock (_processingLock)
            {
                return $"Window Manager Status:\n" +
                       $"- Enabled: {_isEnabled}\n" +
                       $"- Monitoring: {_eventMonitor.IsMonitoring}\n" +
                       $"- Processed Windows: {_processedWindows.Count}\n" +
                       $"- Use Polling: {_eventMonitor.UsePollingFallback}\n" +
                       $"- Polling Interval: {_eventMonitor.PollingIntervalMs}ms\n\n" +
                       _monitorManager.GetMonitorDebugInfo();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                StopMonitoring();
                _eventMonitor?.Dispose();
                _disposed = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing window manager: {ex.Message}");
            }

            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Event arguments for processed windows
    /// </summary>
    public class WindowProcessedEventArgs : EventArgs
    {
        public WindowInfo WindowInfo { get; }
        public WindowTarget Target { get; }
        public DateTime ProcessedAt { get; }

        public WindowProcessedEventArgs(WindowInfo windowInfo, WindowTarget target)
        {
            WindowInfo = windowInfo;
            Target = target;
            ProcessedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// Event arguments for excluded windows
    /// </summary>
    public class WindowExcludedEventArgs : EventArgs
    {
        public WindowInfo WindowInfo { get; }
        public string Reason { get; }
        public DateTime ExcludedAt { get; }

        public WindowExcludedEventArgs(WindowInfo windowInfo, string reason)
        {
            WindowInfo = windowInfo;
            Reason = reason;
            ExcludedAt = DateTime.Now;
        }
    }
}