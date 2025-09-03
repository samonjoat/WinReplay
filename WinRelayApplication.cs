using WinRelay.Services;
using WinRelay.UI;
using WinRelay.Models;

namespace WinRelay
{
    /// <summary>
    /// Main application class that coordinates all services and UI components
    /// </summary>
    public class WinRelayApplication : IDisposable
    {
        private SettingsManager? _settingsManager;
        private WindowManager? _windowManager;
        private HotkeyManager? _hotkeyManager;
        private TrayManager? _trayManager;
        private bool _disposed = false;
        private bool _isInitialized = false;

        public bool IsInitialized => _isInitialized;
        public SettingsManager? SettingsManager => _settingsManager;
        public WindowManager? WindowManager => _windowManager;
        public HotkeyManager? HotkeyManager => _hotkeyManager;
        public TrayManager? TrayManager => _trayManager;

        /// <summary>
        /// Initializes all application services
        /// </summary>
        public void Initialize()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(WinRelayApplication));
            if (_isInitialized) return;

            try
            {
                System.Diagnostics.Debug.WriteLine("Initializing WinRelay application...");

                // Initialize settings manager first
                _settingsManager = new SettingsManager();
                System.Diagnostics.Debug.WriteLine("Settings manager initialized");

                // Initialize window manager
                _windowManager = new WindowManager(_settingsManager);
                System.Diagnostics.Debug.WriteLine("Window manager initialized");

                // Initialize hotkey manager
                _hotkeyManager = new HotkeyManager(_windowManager, _settingsManager);
                System.Diagnostics.Debug.WriteLine("Hotkey manager initialized");

                // Initialize tray manager
                _trayManager = new TrayManager(_settingsManager, _windowManager);
                _trayManager.ShowSettingsRequested += OnShowSettingsRequested;
                _trayManager.ExitRequested += OnExitRequested;
                _trayManager.Initialize();
                System.Diagnostics.Debug.WriteLine("Tray manager initialized");

                // Start services
                StartServices();

                _isInitialized = true;
                System.Diagnostics.Debug.WriteLine("WinRelay application initialized successfully");

                // Show startup notification if enabled
                if (_settingsManager.Settings.UserInterface.ShowTrayNotifications)
                {
                    _trayManager.ShowNotification("WinRelay", 
                        "Window manager started successfully", 
                        ToolTipIcon.Info, 2000);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing WinRelay application: {ex.Message}");
                
                // Clean up on failure
                Dispose();
                throw;
            }
        }

        /// <summary>
        /// Starts all application services
        /// </summary>
        public void StartServices()
        {
            try
            {
                if (!_isInitialized) return;

                // Start window monitoring if auto-center is enabled
                var config = _settingsManager!.Settings.WindowCentering;
                if (config.Mode == CenteringMode.Automatic && config.AutoCenterEnabled)
                {
                    _windowManager!.StartMonitoring();
                    System.Diagnostics.Debug.WriteLine("Window monitoring started");
                }

                // Register global hotkeys
                if (_settingsManager.Settings.Hotkeys.GlobalHotkeysEnabled)
                {
                    _hotkeyManager!.RegisterGlobalHotkeys();
                    System.Diagnostics.Debug.WriteLine("Global hotkeys registered");
                }

                // Update tray state
                _trayManager!.UpdateTrayState();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error starting services: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Stops all application services
        /// </summary>
        public void StopServices()
        {
            try
            {
                _windowManager?.StopMonitoring();
                _hotkeyManager?.UnregisterAllHotkeys();
                System.Diagnostics.Debug.WriteLine("Services stopped");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error stopping services: {ex.Message}");
            }
        }

        /// <summary>
        /// Restarts all services (useful after configuration changes)
        /// </summary>
        public void RestartServices()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Restarting services...");
                StopServices();
                Task.Delay(100).Wait(); // Brief pause
                StartServices();
                System.Diagnostics.Debug.WriteLine("Services restarted successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error restarting services: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles application shutdown
        /// </summary>
        public void Shutdown()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Shutting down WinRelay application...");

                // Save settings
                _settingsManager?.SaveSettings();

                // Stop services
                StopServices();

                // Show shutdown notification
                _trayManager?.ShowNotification("WinRelay", 
                    "Window manager stopped", 
                    ToolTipIcon.Info, 1000);

                // Dispose resources
                Dispose();

                System.Diagnostics.Debug.WriteLine("WinRelay application shutdown complete");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during shutdown: {ex.Message}");
            }
        }

        /// <summary>
        /// Gets application status information
        /// </summary>
        public string GetStatusInfo()
        {
            if (!_isInitialized) return "Application not initialized";

            try
            {
                var info = new System.Text.StringBuilder();
                info.AppendLine("=== WinRelay Status ===");
                info.AppendLine($"Version: {GetApplicationVersion()}");
                info.AppendLine($"Initialized: {_isInitialized}");
                
                if (_settingsManager != null)
                {
                    var config = _settingsManager.Settings.WindowCentering;
                    info.AppendLine($"Auto-Center Mode: {config.Mode}");
                    info.AppendLine($"Window Size: {config.WidthValue}{(config.WidthMode == SizeMode.Percentage ? "%" : "px")} × {config.HeightValue}{(config.HeightMode == SizeMode.Percentage ? "%" : "px")}");
                    info.AppendLine($"Exclusion Rules: {config.ExclusionRules.Count(r => r.Enabled)}");
                    info.AppendLine($"Global Hotkeys: {(_settingsManager.Settings.Hotkeys.GlobalHotkeysEnabled ? "Enabled" : "Disabled")}");
                }

                if (_windowManager != null)
                {
                    info.AppendLine($"Window Monitoring: {(_windowManager.IsMonitoring ? "Active" : "Inactive")}");
                }

                return info.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting status: {ex.Message}";
            }
        }

        private void OnShowSettingsRequested(object? sender, EventArgs e)
        {
            try
            {
                using var settingsForm = new UI.SettingsForm(_settingsManager!, _windowManager!);
                var result = settingsForm.ShowDialog();
                
                if (result == DialogResult.OK)
                {
                    // Restart services to apply new settings
                    RestartServices();
                    _trayManager?.UpdateTrayState();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing settings: {ex.Message}");
                MessageBox.Show($"Error opening settings: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void OnExitRequested(object? sender, EventArgs e)
        {
            try
            {
                Application.Exit();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error handling exit request: {ex.Message}");
                Environment.Exit(1);
            }
        }

        private string GetApplicationVersion()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                return assembly.GetName().Version?.ToString() ?? "1.0.0.0";
            }
            catch
            {
                return "1.0.0.0";
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            try
            {
                StopServices();

                _hotkeyManager?.Dispose();
                _windowManager?.Dispose();
                _settingsManager?.Dispose();
                _trayManager?.Dispose();

                _disposed = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disposing WinRelay application: {ex.Message}");
            }

            GC.SuppressFinalize(this);
        }
    }
}