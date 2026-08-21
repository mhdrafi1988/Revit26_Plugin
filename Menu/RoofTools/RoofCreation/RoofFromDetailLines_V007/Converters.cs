using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoofFromDetailLines.V007
{
    /// <summary>
    /// This tool declares its own copies of BoolToVisibilityConverter and
    /// LogLevelToColorConverter (logic identical to Revit26_Plugin.Shared.Models/
    /// Converters.cs) rather than referencing the shared assembly types directly
    /// in XAML. The shared versions caused a XAML designer failure here
    /// ("does not exist in namespace" / "incompatible type") tied to cross-assembly
    /// xmlns resolution — switching to local copies removed that failure mode
    /// entirely. If your shared Converters.cs is confirmed reachable from this
    /// tool's XAML in your solution, feel free to delete these two classes and
    /// reference the shared ones instead — they are functionally interchangeable.
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v == Visibility.Visible;
    }

    /// <summary>Local copy of LogLevelToColorConverter — see class remarks above.</summary>
    public class LogLevelToColorConverter : IValueConverter
    {
        private readonly SolidColorBrush _brushInfo = new(Color.FromRgb(0x64, 0xD2, 0xFF));
        private readonly SolidColorBrush _brushWarning = new(Color.FromRgb(0xFF, 0x9F, 0x0A));
        private readonly SolidColorBrush _brushError = new(Color.FromRgb(0xFF, 0x45, 0x3A));
        private readonly SolidColorBrush _brushSuccess = new(Color.FromRgb(0x4C, 0xC1, 0x8A));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogLevel level)
                return level switch
                {
                    LogLevel.Warning => _brushWarning,
                    LogLevel.Error => _brushError,
                    LogLevel.Success => _brushSuccess,
                    _ => _brushInfo
                };
            return _brushInfo;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }

    /// <summary>Maps a RoofPreviewRow.StatusKind ("Ready" | "Opening" | "AutoClosed")
    /// to the status-pill background brush. Genuinely tool-specific.</summary>
    public class StatusKindToBrushConverter : IValueConverter
    {
        private static readonly Brush ReadyBrush = new SolidColorBrush(Color.FromRgb(0xE4, 0xF3, 0xE8));
        private static readonly Brush OpeningBrush = new SolidColorBrush(Color.FromRgb(0xE9, 0xEE, 0xF4));
        private static readonly Brush AutoClosedBrush = new SolidColorBrush(Color.FromRgb(0xFB, 0xF1, 0xDC));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return (value as string) switch
            {
                "Opening" => OpeningBrush,
                "AutoClosed" => AutoClosedBrush,
                _ => ReadyBrush
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}


