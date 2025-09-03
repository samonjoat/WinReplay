using WinRelay.Models;

namespace WinRelay.Services
{
    /// <summary>
    /// Calculates target window dimensions and positions
    /// </summary>
    public class SizeCalculator
    {
        private readonly object _calculationLock = new();

        /// <summary>
        /// Calculates target window size based on configuration and monitor
        /// </summary>
        public Size CalculateTargetSize(MonitorInfo monitor, WindowCenteringConfig config)
        {
            lock (_calculationLock)
            {
                var workArea = monitor.WorkArea;
                
                // Calculate width
                int targetWidth = CalculateDimension(
                    workArea.Width, 
                    config.WidthValue, 
                    config.WidthMode);

                // Calculate height  
                int targetHeight = CalculateDimension(
                    workArea.Height, 
                    config.HeightValue, 
                    config.HeightMode);

                // Apply aspect ratio preservation if enabled
                if (config.PreserveAspectRatio)
                {
                    var adjustedSize = PreserveAspectRatio(
                        new Size(targetWidth, targetHeight), 
                        workArea.Size);
                    targetWidth = adjustedSize.Width;
                    targetHeight = adjustedSize.Height;
                }

                // Ensure minimum size constraints
                targetWidth = Math.Max(targetWidth, GetMinimumWidth());
                targetHeight = Math.Max(targetHeight, GetMinimumHeight());

                // Ensure size doesn't exceed work area
                targetWidth = Math.Min(targetWidth, workArea.Width);
                targetHeight = Math.Min(targetHeight, workArea.Height);

                return new Size(targetWidth, targetHeight);
            }
        }

        /// <summary>
        /// Calculates target window size for a specific window
        /// </summary>
        public WindowTarget CalculateWindowTarget(WindowInfo window, MonitorInfo monitor, WindowCenteringConfig config)
        {
            var target = new WindowTarget(monitor)
            {
                WidthMode = config.WidthMode,
                HeightMode = config.HeightMode,
                WidthValue = config.WidthValue,
                HeightValue = config.HeightValue,
                ForceResize = config.ForceResize
            };

            // Calculate target size
            var targetSize = CalculateTargetSize(monitor, config);
            
            // Calculate centered position
            var centerPos = CalculateCenterPosition(monitor, targetSize);
            
            target.Bounds = new Rectangle(centerPos, targetSize);

            return target;
        }

        /// <summary>
        /// Calculates center position for given size on monitor
        /// </summary>
        public Point CalculateCenterPosition(MonitorInfo monitor, Size windowSize)
        {
            var workArea = monitor.WorkArea;
            
            int x = workArea.Left + (workArea.Width - windowSize.Width) / 2;
            int y = workArea.Top + (workArea.Height - windowSize.Height) / 2;

            return new Point(x, y);
        }

        /// <summary>
        /// Calculates dimension (width or height) based on mode and value
        /// </summary>
        public int CalculateDimension(int availableSpace, double value, SizeMode mode)
        {
            return mode switch
            {
                SizeMode.Percentage => (int)(availableSpace * (value / 100.0)),
                SizeMode.FixedPixels => (int)value,
                _ => (int)(availableSpace * 0.8) // Default to 80%
            };
        }

        /// <summary>
        /// Calculates percentage-based width
        /// </summary>
        public int CalculatePercentageWidth(int monitorWidth, double percentage)
        {
            return (int)(monitorWidth * (Math.Clamp(percentage, 1, 100) / 100.0));
        }

        /// <summary>
        /// Calculates percentage-based height
        /// </summary>
        public int CalculatePercentageHeight(int monitorHeight, double percentage)
        {
            return (int)(monitorHeight * (Math.Clamp(percentage, 1, 100) / 100.0));
        }

        /// <summary>
        /// Calculates fixed pixel width with bounds checking
        /// </summary>
        public int CalculateFixedWidth(int monitorWidth, double pixels)
        {
            return (int)Math.Clamp(pixels, GetMinimumWidth(), monitorWidth);
        }

        /// <summary>
        /// Calculates fixed pixel height with bounds checking
        /// </summary>
        public int CalculateFixedHeight(int monitorHeight, double pixels)
        {
            return (int)Math.Clamp(pixels, GetMinimumHeight(), monitorHeight);
        }

        /// <summary>
        /// Preserves aspect ratio while fitting within constraints
        /// </summary>
        public Size PreserveAspectRatio(Size targetSize, Size maxSize)
        {
            if (targetSize.Width <= 0 || targetSize.Height <= 0)
                return targetSize;

            double aspectRatio = (double)targetSize.Width / targetSize.Height;
            
            int newWidth = targetSize.Width;
            int newHeight = targetSize.Height;

            // Check if we need to scale down to fit
            if (newWidth > maxSize.Width)
            {
                newWidth = maxSize.Width;
                newHeight = (int)(newWidth / aspectRatio);
            }

            if (newHeight > maxSize.Height)
            {
                newHeight = maxSize.Height;
                newWidth = (int)(newHeight * aspectRatio);
            }

            return new Size(newWidth, newHeight);
        }

        /// <summary>
        /// Validates if the target size is appropriate for the monitor
        /// </summary>
        public bool IsValidTargetSize(Size targetSize, MonitorInfo monitor)
        {
            var workArea = monitor.WorkArea;
            
            // Check minimum constraints
            if (targetSize.Width < GetMinimumWidth() || targetSize.Height < GetMinimumHeight())
                return false;

            // Check maximum constraints (allow some tolerance)
            if (targetSize.Width > workArea.Width * 1.1 || targetSize.Height > workArea.Height * 1.1)
                return false;

            return true;
        }

        /// <summary>
        /// Calculates the best fit size for a window on a monitor
        /// </summary>
        public Size CalculateBestFit(Size originalSize, MonitorInfo monitor, double maxPercentage = 90.0)
        {
            var workArea = monitor.WorkArea;
            var maxWidth = (int)(workArea.Width * (maxPercentage / 100.0));
            var maxHeight = (int)(workArea.Height * (maxPercentage / 100.0));

            // If window fits, keep original size
            if (originalSize.Width <= maxWidth && originalSize.Height <= maxHeight)
                return originalSize;

            // Scale down while preserving aspect ratio
            double scaleX = (double)maxWidth / originalSize.Width;
            double scaleY = (double)maxHeight / originalSize.Height;
            double scale = Math.Min(scaleX, scaleY);

            int newWidth = Math.Max(GetMinimumWidth(), (int)(originalSize.Width * scale));
            int newHeight = Math.Max(GetMinimumHeight(), (int)(originalSize.Height * scale));

            return new Size(newWidth, newHeight);
        }

        /// <summary>
        /// Calculates size for common presets
        /// </summary>
        public Size CalculatePresetSize(MonitorInfo monitor, WindowPreset preset)
        {
            var workArea = monitor.WorkArea;
            
            return preset switch
            {
                WindowPreset.Small => new Size(
                    (int)(workArea.Width * 0.4),
                    (int)(workArea.Height * 0.5)),
                
                WindowPreset.Medium => new Size(
                    (int)(workArea.Width * 0.6),
                    (int)(workArea.Height * 0.7)),
                
                WindowPreset.Large => new Size(
                    (int)(workArea.Width * 0.8),
                    (int)(workArea.Height * 0.85)),
                
                WindowPreset.ExtraLarge => new Size(
                    (int)(workArea.Width * 0.95),
                    (int)(workArea.Height * 0.95)),
                
                WindowPreset.Square => CalculateSquareSize(workArea),
                
                WindowPreset.Portrait => new Size(
                    (int)(workArea.Width * 0.5),
                    (int)(workArea.Height * 0.8)),
                
                WindowPreset.Landscape => new Size(
                    (int)(workArea.Width * 0.8),
                    (int)(workArea.Height * 0.5)),
                
                _ => new Size(
                    (int)(workArea.Width * 0.75),
                    (int)(workArea.Height * 0.75))
            };
        }

        /// <summary>
        /// Calculates optimal square size for monitor
        /// </summary>
        private Size CalculateSquareSize(Rectangle workArea)
        {
            // Use 70% of the smaller dimension
            int dimension = (int)(Math.Min(workArea.Width, workArea.Height) * 0.7);
            dimension = Math.Max(dimension, GetMinimumWidth());
            return new Size(dimension, dimension);
        }

        /// <summary>
        /// Gets minimum allowed window width
        /// </summary>
        public int GetMinimumWidth() => 200;

        /// <summary>
        /// Gets minimum allowed window height  
        /// </summary>
        public int GetMinimumHeight() => 150;

        /// <summary>
        /// Gets maximum reasonable window width percentage
        /// </summary>
        public double GetMaximumWidthPercentage() => 95.0;

        /// <summary>
        /// Gets maximum reasonable window height percentage
        /// </summary>
        public double GetMaximumHeightPercentage() => 95.0;
    }

    /// <summary>
    /// Common window size presets
    /// </summary>
    public enum WindowPreset
    {
        Small,
        Medium,
        Large,
        ExtraLarge,
        Square,
        Portrait,
        Landscape
    }
}