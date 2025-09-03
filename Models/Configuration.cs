using System.Text.Json.Serialization;

namespace WinRelay.Models
{
    /// <summary>
    /// Configuration for hotkey sequences
    /// </summary>
    public class HotkeySequence
    {
        public ModifierKeys TriggerKey { get; set; } = ModifierKeys.Shift;
        public int PressCount { get; set; } = 3;
        public int TimeoutMs { get; set; } = 1000;
        public bool GlobalScope { get; set; } = true;

        public HotkeySequence() { }

        public HotkeySequence(ModifierKeys triggerKey, int pressCount = 3, int timeoutMs = 1000)
        {
            TriggerKey = triggerKey;
            PressCount = pressCount;
            TimeoutMs = timeoutMs;
        }
    }

    /// <summary>
    /// Window exclusion rule
    /// </summary>
    public class ExclusionRule
    {
        public ExclusionRuleType Type { get; set; }
        public string Pattern { get; set; } = string.Empty;
        public bool IsRegex { get; set; } = false;
        public bool Enabled { get; set; } = true;

        public ExclusionRule() { }

        public ExclusionRule(ExclusionRuleType type, string pattern, bool isRegex = false)
        {
            Type = type;
            Pattern = pattern;
            IsRegex = isRegex;
        }
    }

    /// <summary>
    /// Window centering configuration
    /// </summary>
    public class WindowCenteringConfig
    {
        public bool AutoCenterEnabled { get; set; } = true;
        public CenteringMode Mode { get; set; } = CenteringMode.Automatic;
        
        // Size configuration
        public SizeMode WidthMode { get; set; } = SizeMode.Percentage;
        public SizeMode HeightMode { get; set; } = SizeMode.Percentage;
        public double WidthValue { get; set; } = 88.0;
        public double HeightValue { get; set; } = 75.0;
        
        // Behavior settings
        public bool ForceResize { get; set; } = false;
        public bool PreserveAspectRatio { get; set; } = false;
        public bool SkipMaximizedWindows { get; set; } = true;
        public bool SkipMinimizedWindows { get; set; } = true;
        
        // Exclusion rules
        public List<ExclusionRule> ExclusionRules { get; set; } = new();
        
        // Hotkey configuration
        public HotkeySequence TriggerSequence { get; set; } = new();
        
        // Advanced settings
        public int ProcessingDelayMs { get; set; } = 100;
        public bool ShowConfirmationDialog { get; set; } = false;

        public WindowCenteringConfig()
        {
            // Add default exclusions
            ExclusionRules.Add(new ExclusionRule(ExclusionRuleType.ProcessName, "explorer"));
            ExclusionRules.Add(new ExclusionRule(ExclusionRuleType.ProcessName, "dwm"));
            ExclusionRules.Add(new ExclusionRule(ExclusionRuleType.ProcessName, "winlogon"));
            ExclusionRules.Add(new ExclusionRule(ExclusionRuleType.WindowClass, "Shell_TrayWnd"));
        }
    }

    /// <summary>
    /// Global hotkey configuration
    /// </summary>
    public class HotkeyConfig
    {
        public bool GlobalHotkeysEnabled { get; set; } = true;
        
        // Window management hotkeys
        public string CenterActiveWindow { get; set; } = "Ctrl+Alt+C";
        public string MoveToNextMonitor { get; set; } = "Ctrl+Alt+Right";
        public string MoveToPreviousMonitor { get; set; } = "Ctrl+Alt+Left";
        public string ToggleAlwaysOnTop { get; set; } = "Ctrl+Alt+T";
        
        // Layout hotkeys
        public string ApplyLayout1 { get; set; } = "Ctrl+Alt+1";
        public string ApplyLayout2 { get; set; } = "Ctrl+Alt+2";
        public string ApplyLayout3 { get; set; } = "Ctrl+Alt+3";
        
        // Application hotkeys
        public string ShowSettings { get; set; } = "Ctrl+Alt+S";
        public string ShowHUD { get; set; } = "Ctrl+Alt+H";
        
        public HotkeyConfig() { }
    }

    /// <summary>
    /// User interface configuration
    /// </summary>
    public class UIConfig
    {
        public AppTheme Theme { get; set; } = AppTheme.System;
        public string Language { get; set; } = "en-US";
        public bool MinimizeToTray { get; set; } = true;
        public bool ShowTrayNotifications { get; set; } = true;
        public bool CloseToTray { get; set; } = true;
        public bool ShowHUDOverlay { get; set; } = false;
        
        // Window settings
        public bool RememberWindowPosition { get; set; } = true;
        public int WindowX { get; set; } = -1;
        public int WindowY { get; set; } = -1;
        public int WindowWidth { get; set; } = 600;
        public int WindowHeight { get; set; } = 500;

        public UIConfig() { }
    }

    /// <summary>
    /// Startup and system configuration
    /// </summary>
    public class StartupConfig
    {
        public bool StartWithWindows { get; set; } = true;
        public bool StartMinimized { get; set; } = true;
        public bool CheckForUpdates { get; set; } = true;
        public bool EnableTelemetry { get; set; } = false;
        
        // Performance settings
        public bool UsePollingFallback { get; set; } = false;
        public int PollingIntervalMs { get; set; } = 500;
        public bool EnableDebugLogging { get; set; } = false;

        public StartupConfig() { }
    }

    /// <summary>
    /// Main application settings container
    /// </summary>
    public class ApplicationSettings
    {
        public WindowCenteringConfig WindowCentering { get; set; } = new();
        public HotkeyConfig Hotkeys { get; set; } = new();
        public UIConfig UserInterface { get; set; } = new();
        public StartupConfig Startup { get; set; } = new();
        
        // Metadata
        public string Version { get; set; } = "1.0.0";
        public DateTime LastModified { get; set; } = DateTime.Now;
        public string ConfigurationId { get; set; } = Guid.NewGuid().ToString();

        [JsonIgnore]
        public bool HasUnsavedChanges { get; set; } = false;

        public ApplicationSettings() { }

        /// <summary>
        /// Creates a deep copy of the settings
        /// </summary>
        public ApplicationSettings Clone()
        {
            var json = System.Text.Json.JsonSerializer.Serialize(this);
            var clone = System.Text.Json.JsonSerializer.Deserialize<ApplicationSettings>(json);
            return clone ?? new ApplicationSettings();
        }

        /// <summary>
        /// Validates the configuration
        /// </summary>
        public bool IsValid(out List<string> errors)
        {
            errors = new List<string>();

            // Validate window centering
            if (WindowCentering.WidthValue <= 0)
                errors.Add("Width value must be greater than 0");
            if (WindowCentering.HeightValue <= 0)
                errors.Add("Height value must be greater than 0");
            if (WindowCentering.WidthMode == SizeMode.Percentage && WindowCentering.WidthValue > 100)
                errors.Add("Width percentage cannot exceed 100%");
            if (WindowCentering.HeightMode == SizeMode.Percentage && WindowCentering.HeightValue > 100)
                errors.Add("Height percentage cannot exceed 100%");

            // Validate hotkey sequence
            if (WindowCentering.TriggerSequence.PressCount <= 0)
                errors.Add("Hotkey press count must be greater than 0");
            if (WindowCentering.TriggerSequence.TimeoutMs <= 0)
                errors.Add("Hotkey timeout must be greater than 0");

            // Validate UI settings
            if (UserInterface.WindowWidth <= 0 || UserInterface.WindowHeight <= 0)
                errors.Add("Window dimensions must be positive");

            return errors.Count == 0;
        }

        /// <summary>
        /// Resets settings to defaults
        /// </summary>
        public void ResetToDefaults()
        {
            WindowCentering = new WindowCenteringConfig();
            Hotkeys = new HotkeyConfig();
            UserInterface = new UIConfig();
            Startup = new StartupConfig();
            LastModified = DateTime.Now;
            HasUnsavedChanges = true;
        }
    }
}