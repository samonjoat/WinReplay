using Microsoft.VisualStudio.TestTools.UnitTesting;
using WinRelay.Services;
using WinRelay.Models;

namespace WinRelay.Tests.Services
{
    [TestClass]
    public class SizeCalculatorTests
    {
        private SizeCalculator _calculator;
        private MonitorInfo _testMonitor;

        [TestInitialize]
        public void Setup()
        {
            _calculator = new SizeCalculator();
            _testMonitor = new MonitorInfo
            {
                Handle = IntPtr.Zero,
                Index = 0,
                IsPrimary = true,
                Bounds = new Rectangle(0, 0, 1920, 1080),
                WorkArea = new Rectangle(0, 0, 1920, 1040), // Account for taskbar
                DpiScale = 1.0f,
                DeviceName = "Test Monitor"
            };
        }

        [TestMethod]
        public void CalculateDimension_PercentageMode_ReturnsCorrectPixels()
        {
            // Arrange
            int availableSpace = 1920;
            double percentage = 80.0;

            // Act
            int result = _calculator.CalculateDimension(availableSpace, percentage, SizeMode.Percentage);

            // Assert
            Assert.AreEqual(1536, result); // 1920 * 0.8 = 1536
        }

        [TestMethod]
        public void CalculateDimension_FixedPixelsMode_ReturnsExactValue()
        {
            // Arrange
            int availableSpace = 1920;
            double pixels = 1280;

            // Act
            int result = _calculator.CalculateDimension(availableSpace, pixels, SizeMode.FixedPixels);

            // Assert
            Assert.AreEqual(1280, result);
        }

        [TestMethod]
        public void CalculatePercentageWidth_ValidPercentage_ReturnsCorrectPixels()
        {
            // Arrange
            int monitorWidth = 1920;
            double percentage = 75.0;

            // Act
            int result = _calculator.CalculatePercentageWidth(monitorWidth, percentage);

            // Assert
            Assert.AreEqual(1440, result); // 1920 * 0.75 = 1440
        }

        [TestMethod]
        public void CalculatePercentageWidth_PercentageAbove100_ClampsTo100Percent()
        {
            // Arrange
            int monitorWidth = 1920;
            double percentage = 150.0;

            // Act
            int result = _calculator.CalculatePercentageWidth(monitorWidth, percentage);

            // Assert
            Assert.AreEqual(1920, result); // Should clamp to 100%
        }

        [TestMethod]
        public void CalculateFixedWidth_ValueExceedsMonitor_ClampsToMonitorWidth()
        {
            // Arrange
            int monitorWidth = 1920;
            double pixels = 2500;

            // Act
            int result = _calculator.CalculateFixedWidth(monitorWidth, pixels);

            // Assert
            Assert.AreEqual(1920, result); // Should clamp to monitor width
        }

        [TestMethod]
        public void CalculateFixedWidth_ValueBelowMinimum_ClampsToMinimum()
        {
            // Arrange
            int monitorWidth = 1920;
            double pixels = 50;

            // Act
            int result = _calculator.CalculateFixedWidth(monitorWidth, pixels);

            // Assert
            Assert.AreEqual(_calculator.GetMinimumWidth(), result);
        }

        [TestMethod]
        public void CalculateTargetSize_PercentageMode_ReturnsCorrectSize()
        {
            // Arrange
            var config = new WindowCenteringConfig
            {
                WidthMode = SizeMode.Percentage,
                HeightMode = SizeMode.Percentage,
                WidthValue = 88.0,
                HeightValue = 75.0
            };

            // Act
            var result = _calculator.CalculateTargetSize(_testMonitor, config);

            // Assert
            int expectedWidth = (int)(1920 * 0.88); // 1689
            int expectedHeight = (int)(1040 * 0.75); // 780
            Assert.AreEqual(expectedWidth, result.Width);
            Assert.AreEqual(expectedHeight, result.Height);
        }

        [TestMethod]
        public void CalculateTargetSize_FixedPixelsMode_ReturnsExactSize()
        {
            // Arrange
            var config = new WindowCenteringConfig
            {
                WidthMode = SizeMode.FixedPixels,
                HeightMode = SizeMode.FixedPixels,
                WidthValue = 1280,
                HeightValue = 720
            };

            // Act
            var result = _calculator.CalculateTargetSize(_testMonitor, config);

            // Assert
            Assert.AreEqual(1280, result.Width);
            Assert.AreEqual(720, result.Height);
        }

        [TestMethod]
        public void CalculateCenterPosition_ValidSize_ReturnsCenterCoordinates()
        {
            // Arrange
            var windowSize = new Size(800, 600);

            // Act
            var result = _calculator.CalculateCenterPosition(_testMonitor, windowSize);

            // Assert
            int expectedX = (1920 - 800) / 2; // 560
            int expectedY = (1040 - 600) / 2; // 220
            Assert.AreEqual(expectedX, result.X);
            Assert.AreEqual(expectedY, result.Y);
        }

        [TestMethod]
        public void PreserveAspectRatio_WindowTooWide_ScalesDownCorrectly()
        {
            // Arrange
            var targetSize = new Size(2000, 1000); // 2:1 ratio
            var maxSize = new Size(1600, 1200);

            // Act
            var result = _calculator.PreserveAspectRatio(targetSize, maxSize);

            // Assert
            Assert.AreEqual(1600, result.Width); // Scaled to fit width
            Assert.AreEqual(800, result.Height);  // Maintains 2:1 ratio
        }

        [TestMethod]
        public void PreserveAspectRatio_WindowTooTall_ScalesDownCorrectly()
        {
            // Arrange
            var targetSize = new Size(800, 1600); // 1:2 ratio
            var maxSize = new Size(1200, 1000);

            // Act
            var result = _calculator.PreserveAspectRatio(targetSize, maxSize);

            // Assert
            Assert.AreEqual(500, result.Width);  // Maintains 1:2 ratio
            Assert.AreEqual(1000, result.Height); // Scaled to fit height
        }

        [TestMethod]
        public void IsValidTargetSize_ValidSize_ReturnsTrue()
        {
            // Arrange
            var validSize = new Size(800, 600);

            // Act
            bool result = _calculator.IsValidTargetSize(validSize, _testMonitor);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void IsValidTargetSize_SizeTooSmall_ReturnsFalse()
        {
            // Arrange
            var tooSmallSize = new Size(100, 50);

            // Act
            bool result = _calculator.IsValidTargetSize(tooSmallSize, _testMonitor);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void IsValidTargetSize_SizeTooLarge_ReturnsFalse()
        {
            // Arrange
            var tooLargeSize = new Size(3000, 2000);

            // Act
            bool result = _calculator.IsValidTargetSize(tooLargeSize, _testMonitor);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void CalculatePresetSize_SmallPreset_ReturnsCorrectSize()
        {
            // Arrange
            var preset = WindowPreset.Small;

            // Act
            var result = _calculator.CalculatePresetSize(_testMonitor, preset);

            // Assert
            int expectedWidth = (int)(1920 * 0.4); // 768
            int expectedHeight = (int)(1040 * 0.5); // 520
            Assert.AreEqual(expectedWidth, result.Width);
            Assert.AreEqual(expectedHeight, result.Height);
        }

        [TestMethod]
        public void CalculatePresetSize_LargePreset_ReturnsCorrectSize()
        {
            // Arrange
            var preset = WindowPreset.Large;

            // Act
            var result = _calculator.CalculatePresetSize(_testMonitor, preset);

            // Assert
            int expectedWidth = (int)(1920 * 0.8); // 1536
            int expectedHeight = (int)(1040 * 0.85); // 884
            Assert.AreEqual(expectedWidth, result.Width);
            Assert.AreEqual(expectedHeight, result.Height);
        }

        [TestMethod]
        public void CalculatePresetSize_SquarePreset_ReturnsSquareSize()
        {
            // Arrange
            var preset = WindowPreset.Square;

            // Act
            var result = _calculator.CalculatePresetSize(_testMonitor, preset);

            // Assert
            int expectedDimension = (int)(Math.Min(1920, 1040) * 0.7); // 728
            Assert.AreEqual(expectedDimension, result.Width);
            Assert.AreEqual(expectedDimension, result.Height);
        }

        [TestMethod]
        public void CalculateBestFit_WindowFitsInMonitor_ReturnsOriginalSize()
        {
            // Arrange
            var originalSize = new Size(800, 600);

            // Act
            var result = _calculator.CalculateBestFit(originalSize, _testMonitor);

            // Assert
            Assert.AreEqual(originalSize.Width, result.Width);
            Assert.AreEqual(originalSize.Height, result.Height);
        }

        [TestMethod]
        public void CalculateBestFit_WindowTooLarge_ScalesDown()
        {
            // Arrange
            var oversizedWindow = new Size(2400, 1500); // 1.6:1 ratio

            // Act
            var result = _calculator.CalculateBestFit(oversizedWindow, _testMonitor, 90.0);

            // Assert
            // Should scale down to fit within 90% of monitor while preserving aspect ratio
            Assert.IsTrue(result.Width <= 1920 * 0.9);
            Assert.IsTrue(result.Height <= 1040 * 0.9);
            Assert.IsTrue(result.Width >= _calculator.GetMinimumWidth());
            Assert.IsTrue(result.Height >= _calculator.GetMinimumHeight());
        }
    }
}