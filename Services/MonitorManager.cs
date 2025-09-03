using WinRelay.Native;
using WinRelay.Models;

namespace WinRelay.Services
{
    /// <summary>
    /// Manages monitor enumeration and multi-display functionality
    /// </summary>
    public class MonitorManager
    {
        private readonly List<MonitorInfo> _monitors = new();
        private readonly object _monitorsLock = new();
        private MonitorInfo? _primaryMonitor;
        private DateTime _lastEnumeration = DateTime.MinValue;
        private readonly TimeSpan _cacheValidityPeriod = TimeSpan.FromSeconds(30);

        public event EventHandler? MonitorsChanged;

        /// <summary>
        /// Gets all available monitors
        /// </summary>
        public IReadOnlyList<MonitorInfo> Monitors
        {
            get
            {
                RefreshMonitorsIfNeeded();
                lock (_monitorsLock)
                {
                    return _monitors.AsReadOnly();
                }
            }
        }

        /// <summary>
        /// Gets the primary monitor
        /// </summary>
        public MonitorInfo? PrimaryMonitor
        {
            get
            {
                RefreshMonitorsIfNeeded();
                return _primaryMonitor;
            }
        }

        /// <summary>
        /// Gets the number of available monitors
        /// </summary>
        public int MonitorCount
        {
            get
            {
                RefreshMonitorsIfNeeded();
                lock (_monitorsLock)
                {
                    return _monitors.Count;
                }
            }
        }

        /// <summary>
        /// Refreshes the monitor list
        /// </summary>
        public void RefreshMonitors()
        {
            lock (_monitorsLock)
            {
                var previousCount = _monitors.Count;
                _monitors.Clear();
                _primaryMonitor = null;

                try
                {
                    int monitorIndex = 0;
                    
                    Win32Api.EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, 
                        (IntPtr hMonitor, IntPtr hdcMonitor, ref Win32Api.RECT lprcMonitor, IntPtr dwData) =>
                        {
                            var monitorInfo = new Win32Api.MONITORINFO();
                            monitorInfo.Init();
                            
                            if (Win32Api.GetMonitorInfo(hMonitor, ref monitorInfo))
                            {
                                var monitor = new MonitorInfo(hMonitor, monitorInfo, monitorIndex++);
                                
                                // Try to get DPI information
                                try
                                {
                                    monitor.DpiScale = GetDpiScale(hMonitor);
                                }
                                catch
                                {
                                    monitor.DpiScale = 1.0f; // Default DPI scale
                                }

                                _monitors.Add(monitor);
                                
                                if (monitor.IsPrimary)
                                {
                                    _primaryMonitor = monitor;
                                }
                            }
                            
                            return true; // Continue enumeration
                        }, IntPtr.Zero);

                    _lastEnumeration = DateTime.Now;

                    // Notify if monitor count changed
                    if (_monitors.Count != previousCount)
                    {
                        MonitorsChanged?.Invoke(this, EventArgs.Empty);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error enumerating monitors: {ex.Message}");
                    
                    // Fallback: create a default monitor from primary display
                    CreateFallbackMonitor();
                }
            }
        }

        /// <summary>
        /// Gets monitor containing the specified window
        /// </summary>
        public MonitorInfo? GetMonitorFromWindow(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero) return PrimaryMonitor;

            try
            {
                IntPtr hMonitor = Win32Api.MonitorFromWindow(windowHandle, Win32Api.MONITOR_DEFAULTTONEAREST);
                return GetMonitorByHandle(hMonitor);
            }
            catch
            {
                return PrimaryMonitor;
            }
        }

        /// <summary>
        /// Gets monitor containing the specified point
        /// </summary>
        public MonitorInfo? GetMonitorFromPoint(Point point)
        {
            try
            {
                var pt = new Win32Api.POINT(point.X, point.Y);
                IntPtr hMonitor = Win32Api.MonitorFromPoint(pt, Win32Api.MONITOR_DEFAULTTONEAREST);
                return GetMonitorByHandle(hMonitor);
            }
            catch
            {
                return PrimaryMonitor;
            }
        }

        /// <summary>
        /// Gets monitor containing the specified rectangle
        /// </summary>
        public MonitorInfo? GetMonitorFromRectangle(Rectangle rectangle)
        {
            try
            {
                var rect = new Win32Api.RECT
                {
                    Left = rectangle.Left,
                    Top = rectangle.Top,
                    Right = rectangle.Right,
                    Bottom = rectangle.Bottom
                };
                IntPtr hMonitor = Win32Api.MonitorFromRect(ref rect, Win32Api.MONITOR_DEFAULTTONEAREST);
                return GetMonitorByHandle(hMonitor);
            }
            catch
            {
                return PrimaryMonitor;
            }
        }

        /// <summary>
        /// Gets the next monitor in sequence (for moving windows between displays)
        /// </summary>
        public MonitorInfo? GetNextMonitor(MonitorInfo currentMonitor)
        {
            RefreshMonitorsIfNeeded();
            lock (_monitorsLock)
            {
                if (_monitors.Count <= 1) return currentMonitor;
                
                int currentIndex = _monitors.FindIndex(m => m.Handle == currentMonitor.Handle);
                if (currentIndex == -1) return _monitors.FirstOrDefault();
                
                int nextIndex = (currentIndex + 1) % _monitors.Count;
                return _monitors[nextIndex];
            }
        }

        /// <summary>
        /// Gets the previous monitor in sequence
        /// </summary>
        public MonitorInfo? GetPreviousMonitor(MonitorInfo currentMonitor)
        {
            RefreshMonitorsIfNeeded();
            lock (_monitorsLock)
            {
                if (_monitors.Count <= 1) return currentMonitor;
                
                int currentIndex = _monitors.FindIndex(m => m.Handle == currentMonitor.Handle);
                if (currentIndex == -1) return _monitors.FirstOrDefault();
                
                int prevIndex = currentIndex == 0 ? _monitors.Count - 1 : currentIndex - 1;
                return _monitors[prevIndex];
            }
        }

        /// <summary>
        /// Gets monitor by index
        /// </summary>
        public MonitorInfo? GetMonitorByIndex(int index)
        {
            RefreshMonitorsIfNeeded();
            lock (_monitorsLock)
            {
                return index >= 0 && index < _monitors.Count ? _monitors[index] : null;
            }
        }

        /// <summary>
        /// Gets the largest monitor by work area
        /// </summary>
        public MonitorInfo? GetLargestMonitor()
        {
            RefreshMonitorsIfNeeded();
            lock (_monitorsLock)
            {
                return _monitors.OrderByDescending(m => m.WorkArea.Width * m.WorkArea.Height).FirstOrDefault();
            }
        }

        /// <summary>
        /// Checks if a point is within any monitor bounds
        /// </summary>
        public bool IsPointOnAnyMonitor(Point point)
        {
            RefreshMonitorsIfNeeded();
            lock (_monitorsLock)
            {
                return _monitors.Any(m => m.ContainsPoint(point));
            }
        }

        /// <summary>
        /// Gets total desktop bounds (virtual screen)
        /// </summary>
        public Rectangle GetVirtualScreenBounds()
        {
            RefreshMonitorsIfNeeded();
            lock (_monitorsLock)
            {
                if (_monitors.Count == 0) return Rectangle.Empty;
                
                int left = _monitors.Min(m => m.Bounds.Left);
                int top = _monitors.Min(m => m.Bounds.Top);
                int right = _monitors.Max(m => m.Bounds.Right);
                int bottom = _monitors.Max(m => m.Bounds.Bottom);
                
                return new Rectangle(left, top, right - left, bottom - top);
            }
        }

        /// <summary>
        /// Moves a window to the specified monitor
        /// </summary>
        public bool MoveWindowToMonitor(IntPtr windowHandle, MonitorInfo targetMonitor, bool centerWindow = true)
        {
            try
            {
                if (!Win32Api.GetWindowRect(windowHandle, out var currentRect))
                    return false;

                var currentSize = new Size(currentRect.Width, currentRect.Height);
                Point targetPosition;

                if (centerWindow)
                {
                    // Center on target monitor
                    targetPosition = new Point(
                        targetMonitor.WorkArea.Left + (targetMonitor.WorkArea.Width - currentSize.Width) / 2,
                        targetMonitor.WorkArea.Top + (targetMonitor.WorkArea.Height - currentSize.Height) / 2);
                }
                else
                {
                    // Maintain relative position
                    var currentMonitor = GetMonitorFromWindow(windowHandle);
                    if (currentMonitor != null)
                    {
                        double relativeX = (double)(currentRect.Left - currentMonitor.WorkArea.Left) / currentMonitor.WorkArea.Width;
                        double relativeY = (double)(currentRect.Top - currentMonitor.WorkArea.Top) / currentMonitor.WorkArea.Height;
                        
                        targetPosition = new Point(
                            targetMonitor.WorkArea.Left + (int)(relativeX * targetMonitor.WorkArea.Width),
                            targetMonitor.WorkArea.Top + (int)(relativeY * targetMonitor.WorkArea.Height));
                    }
                    else
                    {
                        targetPosition = targetMonitor.WorkArea.Location;
                    }
                }

                // Ensure window stays within monitor bounds
                targetPosition = EnsureWindowFitsOnMonitor(targetPosition, currentSize, targetMonitor);

                return Win32Api.SetWindowPos(windowHandle, IntPtr.Zero, 
                    targetPosition.X, targetPosition.Y, 0, 0, 
                    Win32Api.SWP_NOSIZE | Win32Api.SWP_NOZORDER);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error moving window to monitor: {ex.Message}");
                return false;
            }
        }

        private MonitorInfo? GetMonitorByHandle(IntPtr hMonitor)
        {
            RefreshMonitorsIfNeeded();
            lock (_monitorsLock)
            {
                return _monitors.FirstOrDefault(m => m.Handle == hMonitor);
            }
        }

        private void RefreshMonitorsIfNeeded()
        {
            if (DateTime.Now - _lastEnumeration > _cacheValidityPeriod)
            {
                RefreshMonitors();
            }
        }

        private float GetDpiScale(IntPtr hMonitor)
        {
            // This is a simplified DPI detection - in a real implementation,
            // you might want to use GetDpiForMonitor from Shcore.dll
            try
            {
                using (var graphics = Graphics.FromHwnd(IntPtr.Zero))
                {
                    float dpiX = graphics.DpiX;
                    return dpiX / 96.0f; // 96 DPI is considered 100% scale
                }
            }
            catch
            {
                return 1.0f; // Default scale
            }
        }

        private void CreateFallbackMonitor()
        {
            try
            {
                // Get primary screen information
                var primaryScreen = Screen.PrimaryScreen;
                if (primaryScreen != null)
                {
                    var monitor = new MonitorInfo
                    {
                        Handle = IntPtr.Zero,
                        Index = 0,
                        IsPrimary = true,
                        Bounds = primaryScreen.Bounds,
                        WorkArea = primaryScreen.WorkingArea,
                        DpiScale = 1.0f,
                        DeviceName = "Primary Display"
                    };
                    
                    _monitors.Add(monitor);
                    _primaryMonitor = monitor;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating fallback monitor: {ex.Message}");
            }
        }

        private Point EnsureWindowFitsOnMonitor(Point position, Size windowSize, MonitorInfo monitor)
        {
            var workArea = monitor.WorkArea;
            
            // Ensure window doesn't go outside monitor bounds
            int x = Math.Max(workArea.Left, Math.Min(position.X, workArea.Right - windowSize.Width));
            int y = Math.Max(workArea.Top, Math.Min(position.Y, workArea.Bottom - windowSize.Height));
            
            return new Point(x, y);
        }

        /// <summary>
        /// Forces a refresh and returns monitor information for debugging
        /// </summary>
        public string GetMonitorDebugInfo()
        {
            RefreshMonitors();
            lock (_monitorsLock)
            {
                var info = new System.Text.StringBuilder();
                info.AppendLine($"Total Monitors: {_monitors.Count}");
                info.AppendLine($"Primary Monitor: {_primaryMonitor?.ToString() ?? "None"}");
                info.AppendLine();
                
                for (int i = 0; i < _monitors.Count; i++)
                {
                    var monitor = _monitors[i];
                    info.AppendLine($"Monitor {i + 1}:");
                    info.AppendLine($"  Handle: {monitor.Handle}");
                    info.AppendLine($"  Primary: {monitor.IsPrimary}");
                    info.AppendLine($"  Bounds: {monitor.Bounds}");
                    info.AppendLine($"  Work Area: {monitor.WorkArea}");
                    info.AppendLine($"  DPI Scale: {monitor.DpiScale:F2}");
                    info.AppendLine();
                }
                
                return info.ToString();
            }
        }
    }
}