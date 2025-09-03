using Microsoft.Win32;

namespace WinRelay.Services
{
    /// <summary>
    /// Manages Windows startup configuration via registry entries
    /// </summary>
    public static class StartupManager
    {
        private const string RegistryKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
        private const string ApplicationName = "WinRelay";

        /// <summary>
        /// Enables auto-start with Windows by adding registry entry
        /// </summary>
        public static bool EnableAutoStart()
        {
            try
            {
                var executablePath = GetExecutablePath();
                if (string.IsNullOrEmpty(executablePath))
                {
                    System.Diagnostics.Debug.WriteLine("Cannot enable auto-start: executable path not found");
                    return false;
                }

                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                if (key == null)
                {
                    System.Diagnostics.Debug.WriteLine("Cannot access startup registry key");
                    return false;
                }

                key.SetValue(ApplicationName, $"\"{executablePath}\"");
                System.Diagnostics.Debug.WriteLine($"Auto-start enabled: {executablePath}");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error enabling auto-start: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Disables auto-start with Windows by removing registry entry
        /// </summary>
        public static bool DisableAutoStart()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, true);
                if (key == null)
                {
                    System.Diagnostics.Debug.WriteLine("Cannot access startup registry key");
                    return false;
                }

                if (key.GetValue(ApplicationName) != null)
                {
                    key.DeleteValue(ApplicationName, false);
                    System.Diagnostics.Debug.WriteLine("Auto-start disabled");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error disabling auto-start: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Checks if auto-start is currently enabled
        /// </summary>
        public static bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
                if (key == null) return false;

                var value = key.GetValue(ApplicationName) as string;
                if (string.IsNullOrEmpty(value)) return false;

                // Check if the registered path matches current executable
                var currentPath = GetExecutablePath();
                var registeredPath = value.Trim('"');
                
                return string.Equals(registeredPath, currentPath, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking auto-start status: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Updates auto-start configuration based on settings
        /// </summary>
        public static bool UpdateAutoStartConfiguration(bool enabled)
        {
            try
            {
                if (enabled)
                {
                    return EnableAutoStart();
                }
                else
                {
                    return DisableAutoStart();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error updating auto-start configuration: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets the current executable path
        /// </summary>
        private static string GetExecutablePath()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                return assembly.Location;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error getting executable path: {ex.Message}");
                return string.Empty;
            }
        }

        /// <summary>
        /// Gets startup configuration information for debugging
        /// </summary>
        public static string GetStartupDebugInfo()
        {
            try
            {
                var info = new System.Text.StringBuilder();
                info.AppendLine("=== Startup Configuration ===");
                info.AppendLine($"Auto-Start Enabled: {IsAutoStartEnabled()}");
                info.AppendLine($"Executable Path: {GetExecutablePath()}");
                
                using var key = Registry.CurrentUser.OpenSubKey(RegistryKeyPath, false);
                if (key != null)
                {
                    var value = key.GetValue(ApplicationName) as string;
                    info.AppendLine($"Registry Entry: {value ?? "Not found"}");
                }
                else
                {
                    info.AppendLine("Registry Key: Not accessible");
                }
                
                return info.ToString();
            }
            catch (Exception ex)
            {
                return $"Error getting startup debug info: {ex.Message}";
            }
        }
    }
}