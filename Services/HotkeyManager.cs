using WinRelay.Native;
using WinRelay.Models;

namespace WinRelay.Services
{
    /// <summary>
    /// Manages global hotkeys and hotkey sequences
    /// </summary>
    public class HotkeyManager : NativeWindow, IDisposable
    {
        private readonly Dictionary<int, HotkeyAction> _registeredHotkeys = new();
        private readonly Dictionary<ModifierKeys, SequenceTracker> _sequenceTrackers = new();
        private readonly WindowManager _windowManager;
        private readonly SettingsManager _settingsManager;
        private readonly object _hotkeyLock = new();
        
        private int _nextHotkeyId = 1000;
        private bool _disposed = false;
        private bool _globalHotkeysEnabled = true;

        public event EventHandler<HotkeyPressedEventArgs>? HotkeyPressed;
        public event EventHandler<SequenceCompletedEventArgs>? SequenceCompleted;

        public bool GlobalHotkeysEnabled
        {
            get => _globalHotkeysEnabled;
            set
            {
                _globalHotkeysEnabled = value;
                if (value)
                {
                    RegisterGlobalHotkeys();
                }
                else
                {
                    UnregisterAllHotkeys();
                }
            }
        }

        public HotkeyManager(WindowManager windowManager, SettingsManager settingsManager)
        {
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));

            // Create window handle for message processing
            CreateHandle(new CreateParams());

            // Subscribe to settings changes
            _settingsManager.SettingsChanged += OnSettingsChanged;

            // Initialize sequence trackers
            InitializeSequenceTrackers();
        }

        /// <summary>
        /// Registers all global hotkeys from configuration
        /// </summary>
        public void RegisterGlobalHotkeys()
        {
            if (!_globalHotkeysEnabled) return;

            lock (_hotkeyLock)
            {
                try
                {
                    // Clear existing hotkeys
                    UnregisterAllHotkeys();

                    var hotkeyConfig = _settingsManager.Settings.Hotkeys;
                    var windowCenteringConfig = _settingsManager.Settings.WindowCentering;

                    // Register global hotkeys
                    RegisterHotkey(hotkeyConfig.CenterActiveWindow, () => _windowManager.CenterActiveWindow());
                    RegisterHotkey(hotkeyConfig.MoveToNextMonitor, () => MoveActiveWindowToNextMonitor());
                    RegisterHotkey(hotkeyConfig.MoveToPreviousMonitor, () => MoveActiveWindowToPreviousMonitor());
                    RegisterHotkey(hotkeyConfig.ToggleAlwaysOnTop, () => ToggleActiveWindowAlwaysOnTop());

                    // Register layout hotkeys
                    RegisterHotkey(hotkeyConfig.ApplyLayout1, () => ApplyPresetToActiveWindow(WindowPreset.Small));
                    RegisterHotkey(hotkeyConfig.ApplyLayout2, () => ApplyPresetToActiveWindow(WindowPreset.Medium));
                    RegisterHotkey(hotkeyConfig.ApplyLayout3, () => ApplyPresetToActiveWindow(WindowPreset.Large));

                    // Setup sequence tracking for window centering
                    if (windowCenteringConfig.Mode == CenteringMode.HotkeyOnly)
                    {
                        SetupSequenceTracking(windowCenteringConfig.TriggerSequence);
                    }

                    System.Diagnostics.Debug.WriteLine($"Registered {_registeredHotkeys.Count} global hotkeys");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error registering global hotkeys: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Registers a single hotkey with action
        /// </summary>
        public bool RegisterHotkey(string hotkeyString, Action action)
        {
            if (string.IsNullOrWhiteSpace(hotkeyString) || action == null) return false;

            try
            {
                if (ParseHotkeyString(hotkeyString, out var modifiers, out var key))
                {
                    int hotkeyId = _nextHotkeyId++;
                    
                    if (Win32Api.RegisterHotKey(Handle, hotkeyId, (uint)modifiers, (uint)key))
                    {
                        _registeredHotkeys[hotkeyId] = new HotkeyAction(hotkeyString, action);
                        return true;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to register hotkey: {hotkeyString}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error registering hotkey '{hotkeyString}': {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// Unregisters all hotkeys
        /// </summary>
        public void UnregisterAllHotkeys()
        {
            lock (_hotkeyLock)
            {
                try
                {
                    foreach (var hotkeyId in _registeredHotkeys.Keys.ToList())
                    {
                        Win32Api.UnregisterHotKey(Handle, hotkeyId);
                    }
                    _registeredHotkeys.Clear();
                    System.Diagnostics.Debug.WriteLine("All hotkeys unregistered");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error unregistering hotkeys: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Processes Windows messages for hotkey detection
        /// </summary>
        protected override void WndProc(ref Message m)
        {
            try
            {
                if (m.Msg == Win32Api.WM_HOTKEY && _globalHotkeysEnabled)
                {
                    int hotkeyId = m.WParam.ToInt32();
                    
                    lock (_hotkeyLock)
                    {
                        if (_registeredHotkeys.TryGetValue(hotkeyId, out var hotkeyAction))
                        {
                            Task.Run(() =>
                            {
                                try
                                {
                                    hotkeyAction.Action.Invoke();
                                    HotkeyPressed?.Invoke(this, new HotkeyPressedEventArgs(hotkeyAction.KeyCombination));
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine($"Error executing hotkey action: {ex.Message}");
                                }
                            });
                        }
                    }
                    return;
                }

                // Check for sequence key presses
                if (m.Msg == 0x0100) // WM_KEYDOWN
                {
                    ProcessSequenceKeyPress((Keys)m.WParam.ToInt32());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in WndProc: {ex.Message}");
            }

            base.WndProc(ref m);
        }

        private void InitializeSequenceTrackers()
        {
            // Initialize trackers for all modifier keys
            foreach (ModifierKeys modifier in Enum.GetValues<ModifierKeys>())
            {
                if (modifier != ModifierKeys.None)
                {
                    _sequenceTrackers[modifier] = new SequenceTracker();
                }
            }
        }

        private void SetupSequenceTracking(HotkeySequence sequence)
        {
            if (_sequenceTrackers.TryGetValue(sequence.TriggerKey, out var tracker))
            {
                tracker.Configure(sequence);
            }
        }

        private void ProcessSequenceKeyPress(Keys key)
        {
            try
            {
                var modifierKey = GetModifierFromKey(key);
                if (modifierKey == ModifierKeys.None) return;

                if (_sequenceTrackers.TryGetValue(modifierKey, out var tracker))
                {
                    if (tracker.ProcessKeyPress())
                    {
                        // Sequence completed
                        SequenceCompleted?.Invoke(this, new SequenceCompletedEventArgs(modifierKey, tracker.Sequence));
                        
                        // Trigger center window action
                        Task.Run(() => _windowManager.CenterActiveWindow());
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error processing sequence key press: {ex.Message}");
            }
        }

        private ModifierKeys GetModifierFromKey(Keys key)
        {
            return key switch
            {
                Keys.LShiftKey or Keys.RShiftKey or Keys.ShiftKey => ModifierKeys.Shift,
                Keys.LControlKey or Keys.RControlKey or Keys.ControlKey => ModifierKeys.Control,
                Keys.LMenu or Keys.RMenu or Keys.Menu => ModifierKeys.Alt,
                Keys.LWin or Keys.RWin => ModifierKeys.Windows,
                _ => ModifierKeys.None
            };
        }

        private bool ParseHotkeyString(string hotkeyString, out uint modifiers, out Keys key)
        {
            modifiers = 0;
            key = Keys.None;

            try
            {
                var parts = hotkeyString.Split('+', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0) return false;

                // Last part is the key
                var keyPart = parts[^1].Trim();
                if (!Enum.TryParse<Keys>(keyPart, true, out key)) return false;

                // Previous parts are modifiers
                for (int i = 0; i < parts.Length - 1; i++)
                {
                    var modifier = parts[i].Trim().ToLowerInvariant();
                    modifiers |= modifier switch
                    {
                        "ctrl" or "control" => Win32Api.MOD_CONTROL,
                        "alt" => Win32Api.MOD_ALT,
                        "shift" => Win32Api.MOD_SHIFT,
                        "win" or "windows" => Win32Api.MOD_WIN,
                        _ => 0
                    };
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void MoveActiveWindowToNextMonitor()
        {
            var activeWindow = Win32Api.GetForegroundWindow();
            if (activeWindow != IntPtr.Zero)
            {
                _windowManager.MoveWindowToNextMonitor(activeWindow);
            }
        }

        private void MoveActiveWindowToPreviousMonitor()
        {
            var activeWindow = Win32Api.GetForegroundWindow();
            if (activeWindow != IntPtr.Zero)
            {
                _windowManager.MoveWindowToPreviousMonitor(activeWindow);
            }
        }

        private void ToggleActiveWindowAlwaysOnTop()
        {
            try
            {
                var activeWindow = Win32Api.GetForegroundWindow();
                if (activeWindow == IntPtr.Zero) return;

                // Get current window position
                if (!Win32Api.GetWindowRect(activeWindow, out var rect)) return;

                // Toggle always on top by setting HWND_TOPMOST or HWND_NOTOPMOST
                const int HWND_TOPMOST = -1;
                const int HWND_NOTOPMOST = -2;

                // Check current Z-order (simplified check)
                var currentStyle = Win32Api.GetWindowLong(activeWindow, -20); // GWL_EXSTYLE
                const long WS_EX_TOPMOST = 0x00000008L;
                bool isCurrentlyTopmost = (currentStyle & WS_EX_TOPMOST) != 0;

                IntPtr insertAfter = isCurrentlyTopmost ? new IntPtr(HWND_NOTOPMOST) : new IntPtr(HWND_TOPMOST);
                
                Win32Api.SetWindowPos(activeWindow, insertAfter, 0, 0, 0, 0,
                    Win32Api.SWP_NOMOVE | Win32Api.SWP_NOSIZE);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error toggling always on top: {ex.Message}");
            }
        }

        private void ApplyPresetToActiveWindow(WindowPreset preset)
        {
            var activeWindow = Win32Api.GetForegroundWindow();
            if (activeWindow != IntPtr.Zero)
            {
                _windowManager.ResizeWindowToPreset(activeWindow, preset);
            }
        }

        private void OnSettingsChanged(object? sender, ApplicationSettings settings)
        {
            GlobalHotkeysEnabled = settings.Hotkeys.GlobalHotkeysEnabled;
            if (_globalHotkeysEnabled)
            {
                RegisterGlobalHotkeys();
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                UnregisterAllHotkeys();
                DestroyHandle();
                _disposed = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing hotkey manager: {ex.Message}");
            }

            GC.SuppressFinalize(this);
        }
    }

    /// <summary>
    /// Tracks hotkey sequences for a specific modifier key
    /// </summary>
    internal class SequenceTracker
    {
        public HotkeySequence Sequence { get; private set; } = new();
        private int _currentCount = 0;
        private DateTime _lastPress = DateTime.MinValue;

        public void Configure(HotkeySequence sequence)
        {
            Sequence = sequence;
            Reset();
        }

        public bool ProcessKeyPress()
        {
            var now = DateTime.Now;
            
            if ((now - _lastPress).TotalMilliseconds > Sequence.TimeoutMs)
            {
                _currentCount = 1;
            }
            else
            {
                _currentCount++;
            }

            _lastPress = now;

            if (_currentCount >= Sequence.PressCount)
            {
                Reset();
                return true; // Sequence completed
            }

            return false;
        }

        public void Reset()
        {
            _currentCount = 0;
            _lastPress = DateTime.MinValue;
        }
    }

    /// <summary>
    /// Represents a hotkey action
    /// </summary>
    internal class HotkeyAction
    {
        public string KeyCombination { get; }
        public Action Action { get; }

        public HotkeyAction(string keyCombination, Action action)
        {
            KeyCombination = keyCombination;
            Action = action;
        }
    }

    /// <summary>
    /// Event arguments for hotkey press events
    /// </summary>
    public class HotkeyPressedEventArgs : EventArgs
    {
        public string KeyCombination { get; }
        public DateTime PressedAt { get; }

        public HotkeyPressedEventArgs(string keyCombination)
        {
            KeyCombination = keyCombination;
            PressedAt = DateTime.Now;
        }
    }

    /// <summary>
    /// Event arguments for sequence completion events
    /// </summary>
    public class SequenceCompletedEventArgs : EventArgs
    {
        public ModifierKeys TriggerKey { get; }
        public HotkeySequence Sequence { get; }
        public DateTime CompletedAt { get; }

        public SequenceCompletedEventArgs(ModifierKeys triggerKey, HotkeySequence sequence)
        {
            TriggerKey = triggerKey;
            Sequence = sequence;
            CompletedAt = DateTime.Now;
        }
    }
}