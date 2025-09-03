using Microsoft.VisualStudio.TestTools.UnitTesting;
using WinRelay.Services;
using WinRelay.Models;
using System.Text.Json;

namespace WinRelay.Tests.Services
{
    [TestClass]
    public class SettingsManagerTests
    {
        private string _testSettingsPath;
        private SettingsManager _settingsManager;

        [TestInitialize]
        public void Setup()
        {
            // Create a temporary directory for test settings
            _testSettingsPath = Path.Combine(Path.GetTempPath(), $"WinRelayTests_{Guid.NewGuid()}");
            Directory.CreateDirectory(_testSettingsPath);
            
            // We'll use reflection to set the settings path for testing
            _settingsManager = new SettingsManager();
        }

        [TestCleanup]
        public void Cleanup()
        {
            try
            {
                _settingsManager?.Dispose();
                if (Directory.Exists(_testSettingsPath))
                {
                    Directory.Delete(_testSettingsPath, true);
                }
            }
            catch
            {
                // Cleanup errors are not critical for tests
            }
        }

        [TestMethod]
        public void Settings_InitialLoad_ReturnsDefaultSettings()
        {
            // Act
            var settings = _settingsManager.Settings;

            // Assert
            Assert.IsNotNull(settings);
            Assert.IsNotNull(settings.WindowCentering);
            Assert.IsNotNull(settings.Hotkeys);
            Assert.IsNotNull(settings.UserInterface);
            Assert.IsNotNull(settings.Startup);
        }

        [TestMethod]
        public void Settings_DefaultWindowCentering_HasCorrectValues()
        {
            // Act
            var config = _settingsManager.Settings.WindowCentering;

            // Assert
            Assert.IsTrue(config.AutoCenterEnabled);
            Assert.AreEqual(SizeMode.Percentage, config.WidthMode);
            Assert.AreEqual(SizeMode.Percentage, config.HeightMode);
            Assert.AreEqual(88.0, config.WidthValue);
            Assert.AreEqual(75.0, config.HeightValue);
            Assert.IsFalse(config.ForceResize);
            Assert.IsTrue(config.ExclusionRules.Count > 0);
        }

        [TestMethod]
        public void UpdateWindowCenteringConfig_ValidConfig_UpdatesSettings()
        {
            // Arrange
            var newConfig = new WindowCenteringConfig
            {
                AutoCenterEnabled = false,
                WidthMode = SizeMode.FixedPixels,
                WidthValue = 1280,
                HeightMode = SizeMode.FixedPixels,
                HeightValue = 720,
                ForceResize = true
            };

            // Act
            _settingsManager.UpdateWindowCenteringConfig(newConfig);

            // Assert
            var settings = _settingsManager.Settings;
            Assert.IsFalse(settings.WindowCentering.AutoCenterEnabled);
            Assert.AreEqual(SizeMode.FixedPixels, settings.WindowCentering.WidthMode);
            Assert.AreEqual(1280, settings.WindowCentering.WidthValue);
            Assert.AreEqual(SizeMode.FixedPixels, settings.WindowCentering.HeightMode);
            Assert.AreEqual(720, settings.WindowCentering.HeightValue);
            Assert.IsTrue(settings.WindowCentering.ForceResize);
        }

        [TestMethod]
        public void AddExclusionRule_ValidRule_AddsToList()
        {
            // Arrange
            var initialCount = _settingsManager.Settings.WindowCentering.ExclusionRules.Count;
            var newRule = new ExclusionRule(ExclusionRuleType.ProcessName, "notepad.exe");

            // Act
            _settingsManager.AddExclusionRule(newRule);

            // Assert
            var rules = _settingsManager.Settings.WindowCentering.ExclusionRules;
            Assert.AreEqual(initialCount + 1, rules.Count);
            Assert.IsTrue(rules.Any(r => r.Pattern == "notepad.exe" && r.Type == ExclusionRuleType.ProcessName));
        }

        [TestMethod]
        public void RemoveExclusionRule_ExistingRule_RemovesFromList()
        {
            // Arrange
            var rule = new ExclusionRule(ExclusionRuleType.WindowTitle, "Test Window");
            _settingsManager.AddExclusionRule(rule);
            var countAfterAdd = _settingsManager.Settings.WindowCentering.ExclusionRules.Count;

            // Act
            _settingsManager.RemoveExclusionRule(rule);

            // Assert
            var rules = _settingsManager.Settings.WindowCentering.ExclusionRules;
            Assert.AreEqual(countAfterAdd - 1, rules.Count);
            Assert.IsFalse(rules.Contains(rule));
        }

        [TestMethod]
        public void UpdateHotkeyConfig_ValidConfig_UpdatesSettings()
        {
            // Arrange
            var newHotkeyConfig = new HotkeyConfig
            {
                GlobalHotkeysEnabled = false,
                CenterActiveWindow = "Ctrl+Alt+Z",
                MoveToNextMonitor = "Ctrl+Shift+Right"
            };

            // Act
            _settingsManager.UpdateHotkeyConfig(newHotkeyConfig);

            // Assert
            var settings = _settingsManager.Settings;
            Assert.IsFalse(settings.Hotkeys.GlobalHotkeysEnabled);
            Assert.AreEqual("Ctrl+Alt+Z", settings.Hotkeys.CenterActiveWindow);
            Assert.AreEqual("Ctrl+Shift+Right", settings.Hotkeys.MoveToNextMonitor);
        }

        [TestMethod]
        public void UpdateUIConfig_ValidConfig_UpdatesSettings()
        {
            // Arrange
            var newUIConfig = new UIConfig
            {
                Theme = AppTheme.Dark,
                ShowTrayNotifications = false,
                CloseToTray = false
            };

            // Act
            _settingsManager.UpdateUIConfig(newUIConfig);

            // Assert
            var settings = _settingsManager.Settings;
            Assert.AreEqual(AppTheme.Dark, settings.UserInterface.Theme);
            Assert.IsFalse(settings.UserInterface.ShowTrayNotifications);
            Assert.IsFalse(settings.UserInterface.CloseToTray);
        }

        [TestMethod]
        public void UpdateStartupConfig_ValidConfig_UpdatesSettings()
        {
            // Arrange
            var newStartupConfig = new StartupConfig
            {
                StartWithWindows = false,
                StartMinimized = false,
                CheckForUpdates = false
            };

            // Act
            _settingsManager.UpdateStartupConfig(newStartupConfig);

            // Assert
            var settings = _settingsManager.Settings;
            Assert.IsFalse(settings.Startup.StartWithWindows);
            Assert.IsFalse(settings.Startup.StartMinimized);
            Assert.IsFalse(settings.Startup.CheckForUpdates);
        }

        [TestMethod]
        public void ResetToDefaults_AfterChanges_RestoresDefaultValues()
        {
            // Arrange - Make some changes
            var modifiedConfig = new WindowCenteringConfig
            {
                AutoCenterEnabled = false,
                WidthValue = 50,
                HeightValue = 50
            };
            _settingsManager.UpdateWindowCenteringConfig(modifiedConfig);

            // Act
            _settingsManager.ResetToDefaults();

            // Assert
            var settings = _settingsManager.Settings;
            Assert.IsTrue(settings.WindowCentering.AutoCenterEnabled);
            Assert.AreEqual(88.0, settings.WindowCentering.WidthValue);
            Assert.AreEqual(75.0, settings.WindowCentering.HeightValue);
        }

        [TestMethod]
        public void ApplicationSettings_IsValid_ValidatesCorrectly()
        {
            // Arrange
            var validSettings = new ApplicationSettings();

            // Act
            bool isValid = validSettings.IsValid(out var errors);

            // Assert
            Assert.IsTrue(isValid);
            Assert.AreEqual(0, errors.Count);
        }

        [TestMethod]
        public void ApplicationSettings_IsValid_DetectsInvalidWidthValue()
        {
            // Arrange
            var invalidSettings = new ApplicationSettings();
            invalidSettings.WindowCentering.WidthValue = -10;

            // Act
            bool isValid = invalidSettings.IsValid(out var errors);

            // Assert
            Assert.IsFalse(isValid);
            Assert.IsTrue(errors.Any(e => e.Contains("Width value")));
        }

        [TestMethod]
        public void ApplicationSettings_IsValid_DetectsInvalidPercentage()
        {
            // Arrange
            var invalidSettings = new ApplicationSettings();
            invalidSettings.WindowCentering.WidthMode = SizeMode.Percentage;
            invalidSettings.WindowCentering.WidthValue = 150; // > 100%

            // Act
            bool isValid = invalidSettings.IsValid(out var errors);

            // Assert
            Assert.IsFalse(isValid);
            Assert.IsTrue(errors.Any(e => e.Contains("percentage cannot exceed")));
        }

        [TestMethod]
        public void ApplicationSettings_Clone_CreatesDeepCopy()
        {
            // Arrange
            var original = _settingsManager.Settings;
            original.WindowCentering.WidthValue = 999;

            // Act
            var clone = original.Clone();
            clone.WindowCentering.WidthValue = 111;

            // Assert
            Assert.AreNotEqual(original.WindowCentering.WidthValue, clone.WindowCentering.WidthValue);
            Assert.AreEqual(999, original.WindowCentering.WidthValue);
            Assert.AreEqual(111, clone.WindowCentering.WidthValue);
        }

        [TestMethod]
        public void HotkeySequence_DefaultValues_AreCorrect()
        {
            // Arrange & Act
            var sequence = new HotkeySequence();

            // Assert
            Assert.AreEqual(ModifierKeys.Shift, sequence.TriggerKey);
            Assert.AreEqual(3, sequence.PressCount);
            Assert.AreEqual(1000, sequence.TimeoutMs);
            Assert.IsTrue(sequence.GlobalScope);
        }

        [TestMethod]
        public void ExclusionRule_Constructor_SetsPropertiesCorrectly()
        {
            // Arrange & Act
            var rule = new ExclusionRule(ExclusionRuleType.WindowClass, "TestClass", true);

            // Assert
            Assert.AreEqual(ExclusionRuleType.WindowClass, rule.Type);
            Assert.AreEqual("TestClass", rule.Pattern);
            Assert.IsTrue(rule.IsRegex);
            Assert.IsTrue(rule.Enabled);
        }
    }
}