# WinRelay - Multi-Display Window Manager

A lightweight Windows desktop utility designed to improve window management across single and multiple displays. WinRelay provides intelligent window centering, global hotkeys, and runs seamlessly from the system tray.

## ✨ Features

### 🎯 **Intelligent Window Centering**
- **Automatic Mode**: All new windows are automatically centered as they appear
- **Hotkey Mode**: Windows are centered only when triggered by configurable key sequences
- **Size Control**: Configure window dimensions using percentages (e.g., 88%) or fixed pixels (e.g., 1280×800)
- **Force Resize**: Optional toggle to apply sizing to non-resizable windows
- **Exclusion Rules**: Ignore specific applications by process name, window title, or class

### 🖥️ **Multi-Display Support**
- **Monitor Detection**: Automatic detection and management of multiple monitors
- **Window Movement**: Move windows between displays with hotkeys
- **DPI Awareness**: Proper handling of different monitor DPI scales
- **Relative Positioning**: Maintains window position relationships when moving between monitors

### ⌨️ **Global Hotkeys**
- **Configurable Sequences**: Set up custom hotkey combinations
- **Window Management**: Quick actions for centering, moving, and resizing
- **Preset Layouts**: Apply predefined window sizes instantly
- **Always-on-Top**: Toggle window layering with hotkeys

### 🎛️ **System Tray Integration**
- **Minimize to Tray**: Runs silently in the background
- **Context Menu**: Quick access to all features
- **Status Notifications**: Optional balloon tips for actions
- **Auto-Start**: Automatically starts with Windows

## 🔧 Requirements

- **Operating System**: Windows 10 version 1809 or later / Windows 11
- **Runtime**: .NET 6.0 Runtime (or .NET 6.0 SDK for building)
- **Architecture**: x64 (64-bit)

## 📦 Installation

### Pre-built Release
1. Download the latest release from the releases page
2. Extract `WinRelay.exe` to your desired location
3. Run `WinRelay.exe` - it will start minimized to the system tray
4. Right-click the tray icon to access settings and features

### Building from Source

#### Prerequisites
- [.NET 6.0 SDK](https://dotnet.microsoft.com/download/dotnet/6.0)
- Windows 10/11 with Visual Studio Build Tools or Visual Studio 2022

#### Build Steps
1. Clone the repository:
   ```bash
   git clone <repository-url>
   cd WinRelay
   ```

2. Build the project:
   ```bash
   # Using the build script (recommended)
   .\build.bat release
   
   # Or using dotnet CLI directly
   dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
   ```

3. The executable will be created in the `publish` directory

## 🚀 Quick Start

### First Launch
1. **Start WinRelay** - The application starts minimized to the system tray
2. **Check the Tray** - Look for the WinRelay icon in your system tray
3. **Right-click the Icon** - Access the context menu for quick actions
4. **Enable Auto-Center** - Toggle automatic window centering from the tray menu

### Default Configuration
- **Auto-Center**: Enabled for new windows
- **Window Size**: 88% width, 75% height (percentage mode)
- **Hotkey Sequence**: Triple-tap Shift key for manual centering
- **Exclusions**: System processes (explorer, dwm, etc.) are pre-excluded

### Common Actions
- **Center Active Window**: Right-click tray icon → "Center Active Window"
- **Move to Next Monitor**: Hotkey `Ctrl+Alt+→` (configurable)
- **Apply Size Presets**: Tray menu → "Window Presets"
- **Toggle Auto-Center**: Right-click tray icon → "Enable/Disable Auto-Center"

## ⚙️ Configuration

### Window Centering Settings
- **Mode**: Choose between Automatic, Hotkey-only, or Disabled
- **Width/Height**: Set dimensions as percentage or fixed pixels
- **Force Resize**: Apply sizing to non-resizable windows (use carefully)
- **Exclusions**: Add applications to ignore by process name or window title

### Hotkey Configuration
- **Global Hotkeys**: System-wide key combinations for quick actions
- **Sequence Detection**: Multi-tap sequences (e.g., triple Shift)
- **Timeout Settings**: Configure timing for sequence detection

### Startup Options
- **Auto-Start**: Automatically start with Windows
- **Start Minimized**: Begin in system tray
- **Notifications**: Enable/disable tray notifications

## 🎛️ Advanced Features

### Exclusion Rules
Create rules to exclude specific windows from auto-centering:

- **Process Name**: e.g., `notepad.exe`, `chrome.exe`
- **Window Title**: e.g., "Task Manager", "Control Panel"
- **Regular Expressions**: Advanced pattern matching support

### Multiple Monitor Scenarios
- **Primary Monitor**: Main display for new windows
- **Extended Desktop**: Spans windows across multiple displays
- **Mixed DPI**: Handles different scaling factors properly

### Performance Options
- **Event Monitoring**: Uses efficient Win32 hooks by default
- **Polling Fallback**: Alternative method for compatibility
- **Processing Delay**: Configurable delay for window initialization

## 🛠️ Troubleshooting

### Common Issues

**WinRelay doesn't start**
- Ensure .NET 6.0 Runtime is installed
- Check Windows Event Log for error details
- Run as Administrator if permission issues occur

**Windows not centering automatically**
- Verify Auto-Center is enabled in tray menu
- Check if the application is in the exclusion list
- Ensure the window is a "normal" application window

**Hotkeys not working**
- Check if another application is using the same combination
- Verify Global Hotkeys are enabled in settings
- Try different key combinations

**Multiple instances running**
- WinRelay enforces single instance - check tray for existing icon
- Kill all WinRelay processes and restart if needed

### Debug Information
Access debug information by right-clicking the tray icon and selecting "Debug Info" (available in debug builds).

## 🔐 Security & Privacy

- **No Network Access**: WinRelay operates entirely locally
- **No Data Collection**: No telemetry or usage data is collected
- **Win32 API Only**: Uses official Windows APIs for window management
- **Local Storage**: Settings stored in `%LOCALAPPDATA%\WinRelay\`

## 📁 File Locations

- **Executable**: User-chosen location
- **Settings**: `%LOCALAPPDATA%\WinRelay\settings.json`
- **Backup**: `%LOCALAPPDATA%\WinRelay\settings.backup.json`
- **Startup Entry**: Registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`

## 🤝 Contributing

### Development Setup
1. Install Visual Studio 2022 or VS Code with C# extension
2. Clone the repository
3. Open `WinRelay.csproj` in your IDE
4. Build and run for testing

### Architecture Overview
- **Services Layer**: Core business logic (WindowManager, SettingsManager, etc.)
- **UI Layer**: System tray integration and settings dialogs
- **Native Layer**: Win32 API wrappers and P/Invoke declarations
- **Models Layer**: Configuration classes and data structures

### Testing
- Unit tests for core components
- Integration tests for Win32 API interactions
- Manual testing on different Windows versions and display configurations

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🎯 Roadmap

### Planned Features
- [ ] **Settings UI**: Tabbed configuration window
- [ ] **Custom Layouts**: User-defined window zones (FancyZones-like)
- [ ] **HUD Overlay**: On-screen controls for window management  
- [ ] **Window Profiles**: Save and apply different configurations
- [ ] **Installer Package**: MSI installer with WiX Toolset

### Future Enhancements
- [ ] **Multi-language Support**: Localization for different languages
- [ ] **Themes**: Light/Dark mode support
- [ ] **Advanced Rules**: Complex exclusion and inclusion logic
- [ ] **Statistics**: Usage tracking and analytics (optional)

## 📞 Support

For issues, feature requests, or questions:

1. **Check Documentation**: Review this README and troubleshooting section
2. **Search Issues**: Look for existing issues in the repository
3. **Create New Issue**: Provide detailed description and system information
4. **Community Discussion**: Join discussions for feature requests

## 🙏 Acknowledgments

- **Microsoft**: For Windows API documentation and .NET platform
- **Community**: For feedback, testing, and contributions
- **Inspiration**: PowerToys FancyZones for layout management concepts

---

**WinRelay** - Making Windows management effortless across any display setup.

*Version 1.0.0 | Copyright © 2025*