using System.Globalization;

namespace WinRelay
{
    internal static class Program
    {
        private const string MutexName = "WinRelay_SingleInstance_Mutex_2025";
        private static Mutex? _singleInstanceMutex;
        private static WinRelayApplication? _application;

        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // Configure application settings
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

            // Set culture to invariant for consistent behavior
            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

            try
            {
                // Enforce single instance
                if (!EnsureSingleInstance())
                {
                    ShowExistingInstance();
                    return;
                }

                // Set up global exception handlers
                SetupExceptionHandlers();

                System.Diagnostics.Debug.WriteLine("Starting WinRelay application...");

                // Initialize and start the application
                _application = new WinRelayApplication();
                _application.Initialize();

                System.Diagnostics.Debug.WriteLine("WinRelay application started successfully");

                // Run the application message loop
                Application.Run();

                System.Diagnostics.Debug.WriteLine("Application message loop ended");
            }
            catch (Exception ex)
            {
                HandleFatalException(ex);
            }
            finally
            {
                Cleanup();
            }
        }

        /// <summary>
        /// Ensures only one instance of the application is running
        /// </summary>
        private static bool EnsureSingleInstance()
        {
            try
            {
                _singleInstanceMutex = new Mutex(true, MutexName, out bool createdNew);
                
                if (!createdNew)
                {
                    // Another instance is already running
                    _singleInstanceMutex.Dispose();
                    _singleInstanceMutex = null;
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error checking single instance: {ex.Message}");
                return true; // Allow startup on error
            }
        }

        /// <summary>
        /// Attempts to show the existing instance (if possible)
        /// </summary>
        private static void ShowExistingInstance()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Another instance of WinRelay is already running");

                // Try to find and activate the existing instance's window
                // This is a basic implementation - in a more advanced version,
                // you could use named pipes or other IPC methods to communicate
                var processes = System.Diagnostics.Process.GetProcessesByName("WinRelay");
                if (processes.Length > 0)
                {
                    var existingProcess = processes[0];
                    if (existingProcess.MainWindowHandle != IntPtr.Zero)
                    {
                        // Restore the window if minimized
                        Native.Win32Api.ShowWindow(existingProcess.MainWindowHandle, Native.Win32Api.SW_RESTORE);
                        
                        // Bring to foreground
                        Native.Win32Api.SetWindowPos(existingProcess.MainWindowHandle, 
                            new IntPtr(-1), 0, 0, 0, 0, 
                            Native.Win32Api.SWP_NOMOVE | Native.Win32Api.SWP_NOSIZE);
                    }
                }

                MessageBox.Show(
                    "WinRelay is already running. Check the system tray for the application icon.",
                    "WinRelay",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error showing existing instance: {ex.Message}");
            }
        }

        /// <summary>
        /// Sets up global exception handlers
        /// </summary>
        private static void SetupExceptionHandlers()
        {
            // Handle unhandled exceptions in UI thread
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += OnThreadException;

            // Handle unhandled exceptions in non-UI threads
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            System.Diagnostics.Debug.WriteLine("Exception handlers configured");
        }

        /// <summary>
        /// Handles exceptions in the UI thread
        /// </summary>
        private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"UI Thread Exception: {e.Exception.Message}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {e.Exception.StackTrace}");

                var message = $"An unexpected error occurred:\n\n{e.Exception.Message}\n\n" +
                             "The application will continue running, but you may want to restart it.\n\n" +
                             "Would you like to view detailed error information?";

                var result = MessageBox.Show(message, "WinRelay Error", 
                    MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

                if (result == DialogResult.Yes)
                {
                    ShowDetailedError(e.Exception);
                }
            }
            catch
            {
                // If we can't even show an error message, just log it
                System.Diagnostics.Debug.WriteLine("Failed to handle thread exception");
            }
        }

        /// <summary>
        /// Handles unhandled exceptions in background threads
        /// </summary>
        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var exception = e.ExceptionObject as Exception;
                System.Diagnostics.Debug.WriteLine($"Unhandled Exception: {exception?.Message ?? "Unknown error"}");
                System.Diagnostics.Debug.WriteLine($"Stack Trace: {exception?.StackTrace ?? "No stack trace available"}");

                if (e.IsTerminating)
                {
                    System.Diagnostics.Debug.WriteLine("Application is terminating due to unhandled exception");
                    
                    // Try to save settings before termination
                    try
                    {
                        _application?.SettingsManager?.SaveSettings();
                    }
                    catch { }
                    
                    HandleFatalException(exception);
                }
            }
            catch
            {
                // Last resort logging
                System.Diagnostics.Debug.WriteLine("Failed to handle unhandled exception");
            }
        }

        /// <summary>
        /// Handles fatal exceptions that cause application termination
        /// </summary>
        private static void HandleFatalException(Exception? exception)
        {
            try
            {
                var message = "A fatal error occurred and the application must close.\n\n";
                
                if (exception != null)
                {
                    message += $"Error: {exception.Message}\n\n";
                    message += "Would you like to view detailed error information?";
                    
                    var result = MessageBox.Show(message, "WinRelay Fatal Error", 
                        MessageBoxButtons.YesNo, MessageBoxIcon.Error);
                    
                    if (result == DialogResult.Yes)
                    {
                        ShowDetailedError(exception);
                    }
                }
                else
                {
                    message += "No additional error information is available.";
                    MessageBox.Show(message, "WinRelay Fatal Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch
            {
                // If we can't show the error dialog, just exit
            }
            finally
            {
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// Shows detailed error information
        /// </summary>
        private static void ShowDetailedError(Exception exception)
        {
            try
            {
                var errorDetails = $"Exception Type: {exception.GetType().Name}\n\n" +
                                 $"Message: {exception.Message}\n\n" +
                                 $"Stack Trace:\n{exception.StackTrace}\n\n";

                if (exception.InnerException != null)
                {
                    errorDetails += $"Inner Exception: {exception.InnerException.Message}\n\n" +
                                  $"Inner Stack Trace:\n{exception.InnerException.StackTrace}";
                }

                // Show in a scrollable text box
                var form = new Form
                {
                    Text = "WinRelay Error Details",
                    Size = new Size(600, 400),
                    StartPosition = FormStartPosition.CenterScreen,
                    Icon = SystemIcons.Error
                };

                var textBox = new TextBox
                {
                    Multiline = true,
                    ScrollBars = ScrollBars.Both,
                    ReadOnly = true,
                    Dock = DockStyle.Fill,
                    Text = errorDetails,
                    Font = new Font("Consolas", 9)
                };

                var panel = new Panel { Dock = DockStyle.Bottom, Height = 40 };
                var closeButton = new Button
                {
                    Text = "Close",
                    DialogResult = DialogResult.OK,
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                    Size = new Size(75, 23)
                };
                closeButton.Location = new Point(panel.Width - closeButton.Width - 10, 
                    (panel.Height - closeButton.Height) / 2);

                panel.Controls.Add(closeButton);
                form.Controls.Add(textBox);
                form.Controls.Add(panel);

                form.ShowDialog();
            }
            catch
            {
                // If we can't show detailed error, fall back to simple message
                MessageBox.Show($"Detailed error information:\n\n{exception}", 
                    "Error Details", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Performs application cleanup
        /// </summary>
        private static void Cleanup()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine("Performing application cleanup...");

                // Shutdown the application
                _application?.Shutdown();

                // Release the single instance mutex
                _singleInstanceMutex?.ReleaseMutex();
                _singleInstanceMutex?.Dispose();

                System.Diagnostics.Debug.WriteLine("Application cleanup completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error during cleanup: {ex.Message}");
            }
        }
    }
}