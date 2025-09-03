using WinRelay.Services;
using WinRelay.Models;

namespace WinRelay.UI
{
    /// <summary>
    /// Manages system tray integration and tray menu functionality
    /// </summary>
    public class TrayManager : IDisposable
    {
        private readonly SettingsManager _settingsManager;
        private readonly WindowManager _windowManager;
        private NotifyIcon? _trayIcon;
        private ContextMenuStrip? _contextMenu;
        private Form? _settingsForm;
        private bool _disposed = false;

        public event EventHandler? ShowSettingsRequested;
        public event EventHandler? ExitRequested;

        public TrayManager(SettingsManager settingsManager, WindowManager windowManager)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
        }

        /// <summary>
        /// Initializes the system tray icon and context menu
        /// </summary>
        public void Initialize()
        {
            try
            {
                CreateTrayIcon();
                CreateContextMenu();
                
                _trayIcon!.ContextMenuStrip = _contextMenu;
                _trayIcon.Visible = true;

                System.Diagnostics.Debug.WriteLine("System tray initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing system tray: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Shows a notification in the system tray
        /// </summary>
        public void ShowNotification(string title, string message, ToolTipIcon icon = ToolTipIcon.Info, int timeout = 3000)
        {
            try
            {
                if (_trayIcon != null && _settingsManager.Settings.UserInterface.ShowTrayNotifications)
                {
                    _trayIcon.ShowBalloonTip(timeout, title, message, icon);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing notification: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the tray icon and menu based on current settings
        /// </summary>
        public void UpdateTrayState()
        {
            try
            {
                if (_trayIcon == null || _contextMenu == null) return;

                var config = _settingsManager.Settings.WindowCentering;
                
                // Update auto-center menu item text
                var autoCenterItem = _contextMenu.Items.Find("AutoCenterItem", false).FirstOrDefault();
                if (autoCenterItem is ToolStripMenuItem menuItem)
                {
                    menuItem.Checked = config.AutoCenterEnabled && config.Mode == CenteringMode.Automatic;
                    menuItem.Text = config.AutoCenterEnabled ? "✓ Auto-Center Enabled" : "Enable Auto-Center";
                }

                // Update tray icon tooltip
                string status = config.Mode switch
                {
                    CenteringMode.Automatic => "Auto-centering active",
                    CenteringMode.HotkeyOnly => "Hotkey mode active",
                    CenteringMode.Disabled => "Disabled",
                    _ => "Unknown status"
                };
                
                _trayIcon.Text = $"WinRelay - {status}";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating tray state: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets the settings form reference for show/hide management
        /// </summary>
        public void SetSettingsForm(Form settingsForm)
        {
            _settingsForm = settingsForm;
            
            if (_settingsForm != null)
            {
                _settingsForm.WindowState = FormWindowState.Normal;
                _settingsForm.ShowInTaskbar = false;
            }
        }

        /// <summary>
        /// Shows the settings window
        /// </summary>
        public void ShowSettings()
        {
            try
            {
                if (_settingsForm == null)
                {
                    ShowSettingsRequested?.Invoke(this, EventArgs.Empty);
                    return;
                }

                if (_settingsForm.WindowState == FormWindowState.Minimized)
                {
                    _settingsForm.WindowState = FormWindowState.Normal;
                }

                _settingsForm.Show();
                _settingsForm.BringToFront();
                _settingsForm.Activate();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Hides the settings window to tray
        /// </summary>
        public void HideToTray()
        {
            try
            {
                if (_settingsForm != null)
                {
                    _settingsForm.Hide();
                    _settingsForm.WindowState = FormWindowState.Minimized;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error hiding to tray: {ex.Message}");
            }
        }

        private void CreateTrayIcon()
        {
            _trayIcon = new NotifyIcon()
            {
                Text = "WinRelay - Window Manager",
                Visible = false
            };

            // Try to load custom icon, fallback to default
            try
            {
                var iconPath = Path.Combine(Application.StartupPath, "Resources", "icon.ico");
                if (File.Exists(iconPath))
                {
                    _trayIcon.Icon = new Icon(iconPath);
                }
                else
                {
                    // Use default system icon
                    _trayIcon.Icon = SystemIcons.Application;
                }
            }
            catch
            {
                _trayIcon.Icon = SystemIcons.Application;
            }

            // Event handlers
            _trayIcon.DoubleClick += OnTrayIconDoubleClick;
            _trayIcon.BalloonTipClicked += OnBalloonTipClicked;
        }

        private void CreateContextMenu()
        {
            _contextMenu = new ContextMenuStrip();

            // Center Active Window
            var centerWindowItem = new ToolStripMenuItem("Center Active Window")
            {
                Name = "CenterWindowItem"
            };
            centerWindowItem.Click += (s, e) => CenterActiveWindow();
            _contextMenu.Items.Add(centerWindowItem);

            // Separator
            _contextMenu.Items.Add(new ToolStripSeparator());

            // Toggle Auto-Center
            var autoCenterItem = new ToolStripMenuItem("Enable Auto-Center")
            {
                Name = "AutoCenterItem",
                CheckOnClick = true
            };
            autoCenterItem.Click += OnToggleAutoCenter;
            _contextMenu.Items.Add(autoCenterItem);

            // Move to Next Monitor
            var nextMonitorItem = new ToolStripMenuItem("Move to Next Monitor");
            nextMonitorItem.Click += (s, e) => MoveActiveWindowToNextMonitor();
            _contextMenu.Items.Add(nextMonitorItem);

            // Separator
            _contextMenu.Items.Add(new ToolStripSeparator());

            // Window Size Presets submenu
            var presetsMenu = new ToolStripMenuItem("Window Presets");
            presetsMenu.DropDownItems.Add(CreatePresetItem("Small (40%)", WindowPreset.Small));
            presetsMenu.DropDownItems.Add(CreatePresetItem("Medium (60%)", WindowPreset.Medium));
            presetsMenu.DropDownItems.Add(CreatePresetItem("Large (80%)", WindowPreset.Large));
            presetsMenu.DropDownItems.Add(CreatePresetItem("Square", WindowPreset.Square));
            _contextMenu.Items.Add(presetsMenu);

            // Separator
            _contextMenu.Items.Add(new ToolStripSeparator());

            // Settings
            var settingsItem = new ToolStripMenuItem("Settings...")
            {
                Font = new Font(_contextMenu.Font, FontStyle.Bold)
            };
            settingsItem.Click += (s, e) => ShowSettings();
            _contextMenu.Items.Add(settingsItem);

            // Debug Info (only in debug builds)
#if DEBUG
            var debugItem = new ToolStripMenuItem("Debug Info...");
            debugItem.Click += OnShowDebugInfo;
            _contextMenu.Items.Add(debugItem);
#endif

            // Separator
            _contextMenu.Items.Add(new ToolStripSeparator());

            // About
            var aboutItem = new ToolStripMenuItem("About WinRelay");
            aboutItem.Click += OnShowAbout;
            _contextMenu.Items.Add(aboutItem);

            // Exit
            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += OnExit;
            _contextMenu.Items.Add(exitItem);

            // Update initial state
            UpdateTrayState();
        }

        private ToolStripMenuItem CreatePresetItem(string text, WindowPreset preset)
        {
            var item = new ToolStripMenuItem(text);
            item.Click += (s, e) => ApplyPresetToActiveWindow(preset);
            return item;
        }

        private void OnTrayIconDoubleClick(object? sender, EventArgs e)
        {
            ShowSettings();
        }

        private void OnBalloonTipClicked(object? sender, EventArgs e)
        {
            ShowSettings();
        }

        private void OnToggleAutoCenter(object? sender, EventArgs e)
        {
            try
            {
                var config = _settingsManager.Settings.WindowCentering;
                
                if (config.Mode == CenteringMode.Automatic)
                {
                    config.Mode = CenteringMode.Disabled;
                    config.AutoCenterEnabled = false;
                    ShowNotification("WinRelay", "Auto-center disabled", ToolTipIcon.Info);
                }
                else
                {
                    config.Mode = CenteringMode.Automatic;
                    config.AutoCenterEnabled = true;
                    ShowNotification("WinRelay", "Auto-center enabled", ToolTipIcon.Info);
                }

                _settingsManager.UpdateWindowCenteringConfig(config);
                UpdateTrayState();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error toggling auto-center: {ex.Message}");
                ShowNotification("WinRelay", "Error toggling auto-center", ToolTipIcon.Error);
            }
        }

        private void CenterActiveWindow()
        {
            try
            {
                bool success = _windowManager.CenterActiveWindow();
                if (success)
                {
                    ShowNotification("WinRelay", "Window centered", ToolTipIcon.Info, 1500);
                }
                else
                {
                    ShowNotification("WinRelay", "Could not center window", ToolTipIcon.Warning, 2000);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error centering active window: {ex.Message}");
                ShowNotification("WinRelay", "Error centering window", ToolTipIcon.Error);
            }
        }

        private void MoveActiveWindowToNextMonitor()
        {
            try
            {
                var activeWindow = Native.Win32Api.GetForegroundWindow();
                if (activeWindow != IntPtr.Zero)
                {
                    bool success = _windowManager.MoveWindowToNextMonitor(activeWindow);
                    if (success)
                    {
                        ShowNotification("WinRelay", "Window moved to next monitor", ToolTipIcon.Info, 1500);
                    }
                    else
                    {
                        ShowNotification("WinRelay", "Could not move window", ToolTipIcon.Warning, 2000);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error moving window to next monitor: {ex.Message}");
                ShowNotification("WinRelay", "Error moving window", ToolTipIcon.Error);
            }
        }

        private void ApplyPresetToActiveWindow(WindowPreset preset)
        {
            try
            {
                var activeWindow = Native.Win32Api.GetForegroundWindow();
                if (activeWindow != IntPtr.Zero)
                {
                    bool success = _windowManager.ResizeWindowToPreset(activeWindow, preset);
                    if (success)
                    {
                        ShowNotification("WinRelay", $"Applied {preset} preset", ToolTipIcon.Info, 1500);
                    }
                    else
                    {
                        ShowNotification("WinRelay", "Could not apply preset", ToolTipIcon.Warning, 2000);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error applying preset: {ex.Message}");
                ShowNotification("WinRelay", "Error applying preset", ToolTipIcon.Error);
            }
        }

        private void OnShowDebugInfo(object? sender, EventArgs e)
        {
            try
            {
                var debugInfo = _windowManager.GetDebugInfo();
                MessageBox.Show(debugInfo, "WinRelay Debug Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error getting debug info: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnShowAbout(object? sender, EventArgs e)
        {
            try
            {
                var version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0.0";
                var aboutText = $"WinRelay v{version}\n\n" +
                              "A lightweight Windows desktop utility for intelligent window management across multiple displays.\n\n" +
                              "Features:\n" +
                              "• Intelligent window centering\n" +
                              "• Multi-monitor support\n" +
                              "• Global hotkeys\n" +
                              "• Customizable sizing\n" +
                              "• System tray integration\n\n" +
                              "Copyright © 2025 WinRelay";

                MessageBox.Show(aboutText, "About WinRelay", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing about dialog: {ex.Message}");
            }
        }

        private void OnExit(object? sender, EventArgs e)
        {
            ExitRequested?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }

                _contextMenu?.Dispose();
                _disposed = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing tray manager: {ex.Message}");
            }

            GC.SuppressFinalize(this);
        }
    }
}