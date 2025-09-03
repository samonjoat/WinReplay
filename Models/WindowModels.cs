using WinRelay.Native;

namespace WinRelay.Models
{
    /// <summary>
    /// Represents a monitor/display in the system
    /// </summary>
    public class MonitorInfo
    {
        public IntPtr Handle { get; set; }
        public string DeviceName { get; set; } = string.Empty;
        public Rectangle Bounds { get; set; }
        public Rectangle WorkArea { get; set; }
        public bool IsPrimary { get; set; }
        public float DpiScale { get; set; } = 1.0f;
        public int Index { get; set; }

        public MonitorInfo() { }

        public MonitorInfo(IntPtr handle, Win32Api.MONITORINFO nativeInfo, int index)
        {
            Handle = handle;
            Index = index;
            Bounds = new Rectangle(
                nativeInfo.rcMonitor.Left,
                nativeInfo.rcMonitor.Top,
                nativeInfo.rcMonitor.Width,
                nativeInfo.rcMonitor.Height);
            WorkArea = new Rectangle(
                nativeInfo.rcWork.Left,
                nativeInfo.rcWork.Top,
                nativeInfo.rcWork.Width,
                nativeInfo.rcWork.Height);
            IsPrimary = (nativeInfo.dwFlags & 1) != 0; // MONITORINFOF_PRIMARY = 1
        }

        public override string ToString()
        {
            return $"Monitor {Index + 1}: {WorkArea.Width}x{WorkArea.Height}" +
                   (IsPrimary ? " (Primary)" : "");
        }

        public bool ContainsPoint(Point point)
        {
            return Bounds.Contains(point);
        }

        public bool ContainsWindow(Rectangle windowBounds)
        {
            return Bounds.IntersectsWith(windowBounds);
        }
    }

    /// <summary>
    /// Represents a window's current state and properties
    /// </summary>
    public class WindowInfo
    {
        public IntPtr Handle { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ClassName { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public uint ProcessId { get; set; }
        public Rectangle Bounds { get; set; }
        public bool IsVisible { get; set; }
        public bool IsMaximized { get; set; }
        public bool IsMinimized { get; set; }
        public MonitorInfo? CurrentMonitor { get; set; }

        public WindowInfo() { }

        public WindowInfo(IntPtr handle)
        {
            Handle = handle;
            UpdateFromHandle();
        }

        public void UpdateFromHandle()
        {
            if (Handle == IntPtr.Zero) return;

            Title = Win32Api.GetWindowTitle(Handle);
            ClassName = Win32Api.GetWindowClassName(Handle);
            ProcessName = Win32Api.GetProcessName(Handle);
            IsVisible = Win32Api.IsWindowVisible(Handle);

            if (Win32Api.GetWindowRect(Handle, out var rect))
            {
                Bounds = new Rectangle(rect.Left, rect.Top, rect.Width, rect.Height);
            }

            // Get window placement to determine state
            var placement = new Win32Api.WINDOWPLACEMENT();
            placement.Init();
            if (Win32Api.GetWindowPlacement(Handle, ref placement))
            {
                IsMaximized = placement.showCmd == Win32Api.SW_SHOWMAXIMIZED;
                IsMinimized = placement.showCmd == Win32Api.SW_SHOWMINIMIZED;
            }

            Win32Api.GetWindowThreadProcessId(Handle, out ProcessId);
        }

        public bool IsNormalWindow()
        {
            return Win32Api.IsNormalWindow(Handle);
        }

        public override string ToString()
        {
            return $"{ProcessName}: {Title} ({Bounds.Width}x{Bounds.Height})";
        }
    }

    /// <summary>
    /// Represents calculated target dimensions and position for a window
    /// </summary>
    public class WindowTarget
    {
        public Rectangle Bounds { get; set; }
        public MonitorInfo TargetMonitor { get; set; }
        public SizeMode WidthMode { get; set; }
        public SizeMode HeightMode { get; set; }
        public double WidthValue { get; set; }
        public double HeightValue { get; set; }
        public bool ForceResize { get; set; }
        public DateTime CalculatedAt { get; set; } = DateTime.Now;

        public WindowTarget(MonitorInfo targetMonitor)
        {
            TargetMonitor = targetMonitor;
        }

        public Point CenterPoint => new(
            Bounds.X + Bounds.Width / 2,
            Bounds.Y + Bounds.Height / 2);

        public bool IsValidTarget()
        {
            return Bounds.Width > 0 && Bounds.Height > 0 &&
                   TargetMonitor.WorkArea.Contains(Bounds.Location) &&
                   TargetMonitor.WorkArea.Contains(new Point(Bounds.Right - 1, Bounds.Bottom - 1));
        }
    }
}