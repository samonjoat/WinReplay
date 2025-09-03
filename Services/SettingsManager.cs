using System.Text.Json;
using System.Text.Json.Serialization;
using WinRelay.Models;

namespace WinRelay.Services
{
    /// <summary>
    /// Manages application settings persistence and validation
    /// </summary>
    public class SettingsManager
    {
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
            "WinRelay");
        
        private static readonly string SettingsFilePath = Path.Combine(AppDataFolder, "settings.json");
        private static readonly string BackupFilePath = Path.Combine(AppDataFolder, "settings.backup.json");

        private readonly JsonSerializerOptions _jsonOptions;
        private ApplicationSettings _currentSettings;
        private readonly object _settingsLock = new();
        private readonly FileSystemWatcher? _fileWatcher;

        public event EventHandler<ApplicationSettings>? SettingsChanged;

        public ApplicationSettings Settings
        {
            get
            {
                lock (_settingsLock)
                {
                    return _currentSettings;
                }
            }
        }

        public SettingsManager()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() },
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };

            _currentSettings = new ApplicationSettings();
            
            // Ensure app data directory exists
            EnsureDirectoryExists();
            
            // Load existing settings
            LoadSettings();

            // Set up file watcher for external changes
            try
            {
                _fileWatcher = new FileSystemWatcher(AppDataFolder, "settings.json")
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                    EnableRaisingEvents = true
                };
                _fileWatcher.Changed += OnSettingsFileChanged;
            }
            catch (Exception ex)
            {
                // File watcher is optional, log error but continue
                System.Diagnostics.Debug.WriteLine($"Failed to create file watcher: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads settings from disk
        /// </summary>
        public void LoadSettings()
        {
            lock (_settingsLock)
            {
                try
                {
                    if (File.Exists(SettingsFilePath))
                    {
                        var json = File.ReadAllText(SettingsFilePath);
                        var loadedSettings = JsonSerializer.Deserialize<ApplicationSettings>(json, _jsonOptions);
                        
                        if (loadedSettings != null)
                        {
                            // Validate loaded settings
                            if (loadedSettings.IsValid(out var errors))
                            {
                                _currentSettings = loadedSettings;
                                _currentSettings.HasUnsavedChanges = false;
                            }
                            else
                            {
                                // Settings are invalid, use defaults and log errors
                                System.Diagnostics.Debug.WriteLine($"Invalid settings loaded: {string.Join(", ", errors)}");
                                _currentSettings = new ApplicationSettings();
                                SaveSettings(); // Save valid defaults
                            }
                        }
                    }
                    else
                    {
                        // No settings file exists, create with defaults
                        _currentSettings = new ApplicationSettings();
                        SaveSettings();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error loading settings: {ex.Message}");
                    
                    // Try to load backup
                    if (TryLoadBackup())
                    {
                        System.Diagnostics.Debug.WriteLine("Loaded settings from backup");
                    }
                    else
                    {
                        // Use defaults if backup also fails
                        _currentSettings = new ApplicationSettings();
                        SaveSettings();
                    }
                }
            }

            // Notify listeners of settings change
            SettingsChanged?.Invoke(this, _currentSettings);
        }

        /// <summary>
        /// Saves current settings to disk
        /// </summary>
        public void SaveSettings()
        {
            lock (_settingsLock)
            {
                try
                {
                    // Validate before saving
                    if (!_currentSettings.IsValid(out var errors))
                    {
                        throw new InvalidOperationException($"Cannot save invalid settings: {string.Join(", ", errors)}");
                    }

                    // Create backup of current file
                    CreateBackup();

                    // Update metadata
                    _currentSettings.LastModified = DateTime.Now;
                    _currentSettings.Version = GetApplicationVersion();

                    // Serialize and save
                    var json = JsonSerializer.Serialize(_currentSettings, _jsonOptions);
                    File.WriteAllText(SettingsFilePath, json);

                    _currentSettings.HasUnsavedChanges = false;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error saving settings: {ex.Message}");
                    throw;
                }
            }
        }

        /// <summary>
        /// Updates specific configuration section
        /// </summary>
        public void UpdateWindowCenteringConfig(WindowCenteringConfig config)
        {
            lock (_settingsLock)
            {
                _currentSettings.WindowCentering = config;
                _currentSettings.HasUnsavedChanges = true;
                SaveSettings();
            }
            SettingsChanged?.Invoke(this, _currentSettings);
        }

        /// <summary>
        /// Updates hotkey configuration
        /// </summary>
        public void UpdateHotkeyConfig(HotkeyConfig config)
        {
            lock (_settingsLock)
            {
                _currentSettings.Hotkeys = config;
                _currentSettings.HasUnsavedChanges = true;
                SaveSettings();
            }
            SettingsChanged?.Invoke(this, _currentSettings);
        }

        /// <summary>
        /// Updates UI configuration
        /// </summary>
        public void UpdateUIConfig(UIConfig config)
        {
            lock (_settingsLock)
            {
                _currentSettings.UserInterface = config;
                _currentSettings.HasUnsavedChanges = true;
                SaveSettings();
            }
            SettingsChanged?.Invoke(this, _currentSettings);
        }

        /// <summary>
        /// Updates startup configuration
        /// </summary>
        public void UpdateStartupConfig(StartupConfig config)
        {
            lock (_settingsLock)
            {
                _currentSettings.Startup = config;
                _currentSettings.HasUnsavedChanges = true;
                SaveSettings();
            }
            SettingsChanged?.Invoke(this, _currentSettings);
        }

        /// <summary>
        /// Adds an exclusion rule
        /// </summary>
        public void AddExclusionRule(ExclusionRule rule)
        {
            lock (_settingsLock)
            {
                _currentSettings.WindowCentering.ExclusionRules.Add(rule);
                _currentSettings.HasUnsavedChanges = true;
                SaveSettings();
            }
            SettingsChanged?.Invoke(this, _currentSettings);
        }

        /// <summary>
        /// Removes an exclusion rule
        /// </summary>
        public void RemoveExclusionRule(ExclusionRule rule)
        {
            lock (_settingsLock)
            {
                _currentSettings.WindowCentering.ExclusionRules.Remove(rule);
                _currentSettings.HasUnsavedChanges = true;
                SaveSettings();
            }
            SettingsChanged?.Invoke(this, _currentSettings);
        }

        /// <summary>
        /// Resets settings to defaults
        /// </summary>
        public void ResetToDefaults()
        {
            lock (_settingsLock)
            {
                _currentSettings.ResetToDefaults();
                SaveSettings();
            }
            SettingsChanged?.Invoke(this, _currentSettings);
        }

        /// <summary>
        /// Exports settings to specified file
        /// </summary>
        public void ExportSettings(string filePath)
        {
            lock (_settingsLock)
            {
                var json = JsonSerializer.Serialize(_currentSettings, _jsonOptions);
                File.WriteAllText(filePath, json);
            }
        }

        /// <summary>
        /// Imports settings from specified file
        /// </summary>
        public void ImportSettings(string filePath)
        {
            try
            {
                var json = File.ReadAllText(filePath);
                var importedSettings = JsonSerializer.Deserialize<ApplicationSettings>(json, _jsonOptions);
                
                if (importedSettings != null && importedSettings.IsValid(out var errors))
                {
                    lock (_settingsLock)
                    {
                        _currentSettings = importedSettings;
                        _currentSettings.HasUnsavedChanges = true;
                        SaveSettings();
                    }
                    SettingsChanged?.Invoke(this, _currentSettings);
                }
                else
                {
                    throw new InvalidOperationException($"Invalid settings file: {string.Join(", ", errors ?? new List<string>())}");
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to import settings: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the settings file path
        /// </summary>
        public string GetSettingsFilePath() => SettingsFilePath;

        /// <summary>
        /// Gets the backup file path
        /// </summary>
        public string GetBackupFilePath() => BackupFilePath;

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(AppDataFolder))
            {
                Directory.CreateDirectory(AppDataFolder);
            }
        }

        private void CreateBackup()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    File.Copy(SettingsFilePath, BackupFilePath, true);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to create backup: {ex.Message}");
            }
        }

        private bool TryLoadBackup()
        {
            try
            {
                if (File.Exists(BackupFilePath))
                {
                    var json = File.ReadAllText(BackupFilePath);
                    var backupSettings = JsonSerializer.Deserialize<ApplicationSettings>(json, _jsonOptions);
                    
                    if (backupSettings != null && backupSettings.IsValid(out _))
                    {
                        _currentSettings = backupSettings;
                        _currentSettings.HasUnsavedChanges = true;
                        return true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load backup: {ex.Message}");
            }
            return false;
        }

        private void OnSettingsFileChanged(object sender, FileSystemEventArgs e)
        {
            // Debounce file changes to avoid multiple reloads
            Task.Delay(500).ContinueWith(_ =>
            {
                try
                {
                    LoadSettings();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to reload settings after file change: {ex.Message}");
                }
            });
        }

        private string GetApplicationVersion()
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                var version = assembly.GetName().Version;
                return version?.ToString() ?? "1.0.0.0";
            }
            catch
            {
                return "1.0.0.0";
            }
        }

        public void Dispose()
        {
            _fileWatcher?.Dispose();
        }
    }
}