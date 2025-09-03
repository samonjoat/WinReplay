namespace WinRelay.Models
{
    /// <summary>
    /// Enumeration for window sizing modes
    /// </summary>
    public enum SizeMode
    {
        Percentage,
        FixedPixels
    }

    /// <summary>
    /// Enumeration for modifier keys
    /// </summary>
    [Flags]
    public enum ModifierKeys
    {
        None = 0,
        Alt = 1,
        Control = 2,
        Shift = 4,
        Windows = 8
    }

    /// <summary>
    /// Application theme options
    /// </summary>
    public enum AppTheme
    {
        Light,
        Dark,
        System
    }

    /// <summary>
    /// Window centering trigger modes
    /// </summary>
    public enum CenteringMode
    {
        Automatic,
        HotkeyOnly,
        Disabled
    }

    /// <summary>
    /// Window exclusion rule types
    /// </summary>
    public enum ExclusionRuleType
    {
        ProcessName,
        WindowTitle,
        WindowClass
    }
}