using WinRelay.Services;
using WinRelay.Models;

namespace WinRelay.UI
{
    public partial class SettingsForm : Form
    {
        private readonly SettingsManager _settingsManager;
        private readonly WindowManager _windowManager;
        private TabControl _tabControl;
        private bool _isLoading = false;

        public SettingsForm(SettingsManager settingsManager, WindowManager windowManager)
        {
            _settingsManager = settingsManager ?? throw new ArgumentNullException(nameof(settingsManager));
            _windowManager = windowManager ?? throw new ArgumentNullException(nameof(windowManager));
            
            InitializeComponent();
            LoadSettings();
        }

        private void InitializeComponent()
        {
            Text = "WinRelay Settings";
            Size = new Size(600, 500);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;

            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill
            };

            CreateWindowCenteringTab();
            CreateHotkeysTab();
            CreateGeneralTab();

            var buttonPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 50
            };

            var okButton = new Button
            {
                Text = "OK",
                Size = new Size(75, 23),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.OK
            };
            okButton.Location = new Point(buttonPanel.Width - 170, 15);
            okButton.Click += OnOkClicked;

            var cancelButton = new Button
            {
                Text = "Cancel",
                Size = new Size(75, 23),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.Cancel
            };
            cancelButton.Location = new Point(buttonPanel.Width - 85, 15);

            buttonPanel.Controls.AddRange(new Control[] { okButton, cancelButton });
            Controls.AddRange(new Control[] { _tabControl, buttonPanel });
        }

        private void CreateWindowCenteringTab()
        {
            var tabPage = new TabPage("Window Centering");
            
            var enabledCheckBox = new CheckBox
            {
                Text = "Enable automatic window centering",
                Location = new Point(20, 20),
                Size = new Size(250, 20),
                Tag = "AutoCenterEnabled"
            };

            var modeGroupBox = new GroupBox
            {
                Text = "Centering Mode",
                Location = new Point(20, 50),
                Size = new Size(280, 80)
            };

            var autoRadio = new RadioButton
            {
                Text = "Automatic (all new windows)",
                Location = new Point(10, 20),
                Size = new Size(200, 20),
                Tag = "ModeAutomatic"
            };

            var hotkeyRadio = new RadioButton
            {
                Text = "Hotkey only (manual trigger)",
                Location = new Point(10, 45),
                Size = new Size(200, 20),
                Tag = "ModeHotkey"
            };

            modeGroupBox.Controls.AddRange(new Control[] { autoRadio, hotkeyRadio });

            var sizeGroupBox = new GroupBox
            {
                Text = "Window Size",
                Location = new Point(20, 140),
                Size = new Size(280, 120)
            };

            var widthLabel = new Label
            {
                Text = "Width:",
                Location = new Point(10, 25),
                Size = new Size(45, 20)
            };

            var widthNumeric = new NumericUpDown
            {
                Location = new Point(60, 23),
                Size = new Size(80, 20),
                Minimum = 1,
                Maximum = 9999,
                Tag = "WidthValue"
            };

            var widthModeCombo = new ComboBox
            {
                Location = new Point(150, 23),
                Size = new Size(80, 20),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Tag = "WidthMode"
            };
            widthModeCombo.Items.AddRange(new[] { "Percentage", "Pixels" });

            var heightLabel = new Label
            {
                Text = "Height:",
                Location = new Point(10, 55),
                Size = new Size(45, 20)
            };

            var heightNumeric = new NumericUpDown
            {
                Location = new Point(60, 53),
                Size = new Size(80, 20),
                Minimum = 1,
                Maximum = 9999,
                Tag = "HeightValue"
            };

            var heightModeCombo = new ComboBox
            {
                Location = new Point(150, 53),
                Size = new Size(80, 20),
                DropDownStyle = ComboBoxStyle.DropDownList,
                Tag = "HeightMode"
            };
            heightModeCombo.Items.AddRange(new[] { "Percentage", "Pixels" });

            var forceResizeCheckBox = new CheckBox
            {
                Text = "Force resize non-resizable windows",
                Location = new Point(10, 85),
                Size = new Size(250, 20),
                Tag = "ForceResize"
            };

            sizeGroupBox.Controls.AddRange(new Control[] { 
                widthLabel, widthNumeric, widthModeCombo,
                heightLabel, heightNumeric, heightModeCombo,
                forceResizeCheckBox
            });

            tabPage.Controls.AddRange(new Control[] { enabledCheckBox, modeGroupBox, sizeGroupBox });
            _tabControl.TabPages.Add(tabPage);
        }

        private void CreateHotkeysTab()
        {
            var tabPage = new TabPage("Hotkeys");
            
            var enabledCheckBox = new CheckBox
            {
                Text = "Enable global hotkeys",
                Location = new Point(20, 20),
                Size = new Size(150, 20),
                Tag = "GlobalHotkeysEnabled"
            };

            var hotkeyGroupBox = new GroupBox
            {
                Text = "Hotkey Assignments",
                Location = new Point(20, 50),
                Size = new Size(350, 200)
            };

            var y = 25;
            var hotkeys = new[]
            {
                ("Center Active Window:", "CenterActiveWindow"),
                ("Move to Next Monitor:", "MoveToNextMonitor"),
                ("Move to Previous Monitor:", "MoveToPreviousMonitor"),
                ("Toggle Always on Top:", "ToggleAlwaysOnTop"),
                ("Show Settings:", "ShowSettings")
            };

            foreach (var (label, tag) in hotkeys)
            {
                var lbl = new Label
                {
                    Text = label,
                    Location = new Point(10, y),
                    Size = new Size(150, 20)
                };

                var textBox = new TextBox
                {
                    Location = new Point(170, y - 2),
                    Size = new Size(150, 20),
                    Tag = tag,
                    ReadOnly = true
                };

                hotkeyGroupBox.Controls.AddRange(new Control[] { lbl, textBox });
                y += 30;
            }

            tabPage.Controls.AddRange(new Control[] { enabledCheckBox, hotkeyGroupBox });
            _tabControl.TabPages.Add(tabPage);
        }

        private void CreateGeneralTab()
        {
            var tabPage = new TabPage("General");
            
            var startupGroupBox = new GroupBox
            {
                Text = "Startup",
                Location = new Point(20, 20),
                Size = new Size(280, 100)
            };

            var startWithWindowsCheckBox = new CheckBox
            {
                Text = "Start with Windows",
                Location = new Point(10, 25),
                Size = new Size(150, 20),
                Tag = "StartWithWindows"
            };

            var startMinimizedCheckBox = new CheckBox
            {
                Text = "Start minimized to tray",
                Location = new Point(10, 50),
                Size = new Size(150, 20),
                Tag = "StartMinimized"
            };

            startupGroupBox.Controls.AddRange(new Control[] { startWithWindowsCheckBox, startMinimizedCheckBox });

            var uiGroupBox = new GroupBox
            {
                Text = "User Interface",
                Location = new Point(20, 130),
                Size = new Size(280, 100)
            };

            var showNotificationsCheckBox = new CheckBox
            {
                Text = "Show tray notifications",
                Location = new Point(10, 25),
                Size = new Size(150, 20),
                Tag = "ShowTrayNotifications"
            };

            var closeToTrayCheckBox = new CheckBox
            {
                Text = "Close to tray",
                Location = new Point(10, 50),
                Size = new Size(150, 20),
                Tag = "CloseToTray"
            };

            uiGroupBox.Controls.AddRange(new Control[] { showNotificationsCheckBox, closeToTrayCheckBox });

            tabPage.Controls.AddRange(new Control[] { startupGroupBox, uiGroupBox });
            _tabControl.TabPages.Add(tabPage);
        }

        private void LoadSettings()
        {
            _isLoading = true;
            try
            {
                var settings = _settingsManager.Settings;
                LoadControlValues(this, settings);
            }
            finally
            {
                _isLoading = false;
            }
        }

        private void LoadControlValues(Control parent, ApplicationSettings settings)
        {
            foreach (Control control in parent.Controls)
            {
                if (control.Tag is string tag)
                {
                    LoadControlValue(control, tag, settings);
                }
                LoadControlValues(control, settings);
            }
        }

        private void LoadControlValue(Control control, string tag, ApplicationSettings settings)
        {
            var config = settings.WindowCentering;
            var hotkeys = settings.Hotkeys;
            var startup = settings.Startup;
            var ui = settings.UserInterface;

            switch (tag)
            {
                case "AutoCenterEnabled":
                    ((CheckBox)control).Checked = config.AutoCenterEnabled;
                    break;
                case "ModeAutomatic":
                    ((RadioButton)control).Checked = config.Mode == CenteringMode.Automatic;
                    break;
                case "ModeHotkey":
                    ((RadioButton)control).Checked = config.Mode == CenteringMode.HotkeyOnly;
                    break;
                case "WidthValue":
                    ((NumericUpDown)control).Value = (decimal)config.WidthValue;
                    break;
                case "WidthMode":
                    ((ComboBox)control).SelectedIndex = (int)config.WidthMode;
                    break;
                case "HeightValue":
                    ((NumericUpDown)control).Value = (decimal)config.HeightValue;
                    break;
                case "HeightMode":
                    ((ComboBox)control).SelectedIndex = (int)config.HeightMode;
                    break;
                case "ForceResize":
                    ((CheckBox)control).Checked = config.ForceResize;
                    break;
                case "GlobalHotkeysEnabled":
                    ((CheckBox)control).Checked = hotkeys.GlobalHotkeysEnabled;
                    break;
                case "CenterActiveWindow":
                    ((TextBox)control).Text = hotkeys.CenterActiveWindow;
                    break;
                case "MoveToNextMonitor":
                    ((TextBox)control).Text = hotkeys.MoveToNextMonitor;
                    break;
                case "MoveToPreviousMonitor":
                    ((TextBox)control).Text = hotkeys.MoveToPreviousMonitor;
                    break;
                case "ToggleAlwaysOnTop":
                    ((TextBox)control).Text = hotkeys.ToggleAlwaysOnTop;
                    break;
                case "ShowSettings":
                    ((TextBox)control).Text = hotkeys.ShowSettings;
                    break;
                case "StartWithWindows":
                    ((CheckBox)control).Checked = StartupManager.IsAutoStartEnabled();
                    break;
                case "StartMinimized":
                    ((CheckBox)control).Checked = startup.StartMinimized;
                    break;
                case "ShowTrayNotifications":
                    ((CheckBox)control).Checked = ui.ShowTrayNotifications;
                    break;
                case "CloseToTray":
                    ((CheckBox)control).Checked = ui.CloseToTray;
                    break;
            }
        }

        private void OnOkClicked(object? sender, EventArgs e)
        {
            try
            {
                SaveSettings();
                DialogResult = DialogResult.OK;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving settings: {ex.Message}", "Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SaveSettings()
        {
            var settings = _settingsManager.Settings.Clone();
            SaveControlValues(this, settings);
            
            _settingsManager.UpdateWindowCenteringConfig(settings.WindowCentering);
            _settingsManager.UpdateHotkeyConfig(settings.Hotkeys);
            _settingsManager.UpdateStartupConfig(settings.Startup);
            _settingsManager.UpdateUIConfig(settings.UserInterface);
        }

        private void SaveControlValues(Control parent, ApplicationSettings settings)
        {
            foreach (Control control in parent.Controls)
            {
                if (control.Tag is string tag)
                {
                    SaveControlValue(control, tag, settings);
                }
                SaveControlValues(control, settings);
            }
        }

        private void SaveControlValue(Control control, string tag, ApplicationSettings settings)
        {
            var config = settings.WindowCentering;
            var hotkeys = settings.Hotkeys;
            var startup = settings.Startup;
            var ui = settings.UserInterface;

            switch (tag)
            {
                case "AutoCenterEnabled":
                    config.AutoCenterEnabled = ((CheckBox)control).Checked;
                    break;
                case "ModeAutomatic":
                    if (((RadioButton)control).Checked)
                        config.Mode = CenteringMode.Automatic;
                    break;
                case "ModeHotkey":
                    if (((RadioButton)control).Checked)
                        config.Mode = CenteringMode.HotkeyOnly;
                    break;
                case "WidthValue":
                    config.WidthValue = (double)((NumericUpDown)control).Value;
                    break;
                case "WidthMode":
                    config.WidthMode = (SizeMode)((ComboBox)control).SelectedIndex;
                    break;
                case "HeightValue":
                    config.HeightValue = (double)((NumericUpDown)control).Value;
                    break;
                case "HeightMode":
                    config.HeightMode = (SizeMode)((ComboBox)control).SelectedIndex;
                    break;
                case "ForceResize":
                    config.ForceResize = ((CheckBox)control).Checked;
                    break;
                case "GlobalHotkeysEnabled":
                    hotkeys.GlobalHotkeysEnabled = ((CheckBox)control).Checked;
                    break;
                case "StartWithWindows":
                    var startWithWindows = ((CheckBox)control).Checked;
                    startup.StartWithWindows = startWithWindows;
                    StartupManager.UpdateAutoStartConfiguration(startWithWindows);
                    break;
                case "StartMinimized":
                    startup.StartMinimized = ((CheckBox)control).Checked;
                    break;
                case "ShowTrayNotifications":
                    ui.ShowTrayNotifications = ((CheckBox)control).Checked;
                    break;
                case "CloseToTray":
                    ui.CloseToTray = ((CheckBox)control).Checked;
                    break;
            }
        }
    }
}