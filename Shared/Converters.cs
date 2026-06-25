using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Revit26_Plugin.Shared.Models
{
    /// <summary>
    /// Converts a bool value to Visibility (true → Visible, false → Collapsed).
    /// Used for conditional visibility bindings throughout tools.
    /// </summary>
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v == Visibility.Visible;
    }

    /// <summary>
    /// Converts a bool value to its inverse Visibility (true → Collapsed, false → Visible).
    /// Used for inverse visibility bindings (e.g., show when NOT busy).
    /// </summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && !b) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v != Visibility.Visible;
    }

    /// <summary>
    /// Converts an int value to Visibility (0 → Collapsed, non-zero → Visible).
    /// Used for hiding empty states when lists have items.
    /// </summary>
    public class IntToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is int i && i == 0) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }

    /// <summary>
    /// Converts a bool value to its inverse (true → false, false → true).
    /// Used for IsEnabled bindings that should invert state (e.g., disable when busy).
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : false;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : false;
    }

    /// <summary>
    /// Maps LogLevel to a colored brush for log display.
    /// INFO → cyan, WARNING → orange, ERROR → red, SUCCESS → green.
    /// </summary>
    public class LogLevelToColorConverter : IValueConverter
    {
        // Instance brushes — avoids static initializer running off the UI thread in Revit,
        // which caused StaticResourceExtension to throw on first converter instantiation.
        private readonly SolidColorBrush _brushInfo    = new(Color.FromRgb(0x64, 0xD2, 0xFF));
        private readonly SolidColorBrush _brushWarning = new(Color.FromRgb(0xFF, 0x9F, 0x0A));
        private readonly SolidColorBrush _brushError   = new(Color.FromRgb(0xFF, 0x45, 0x3A));
        private readonly SolidColorBrush _brushSuccess = new(Color.FromRgb(0x4C, 0xC1, 0x8A));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogLevel level)
                return level switch
                {
                    LogLevel.Warning => _brushWarning,
                    LogLevel.Error   => _brushError,
                    LogLevel.Success => _brushSuccess,
                    _                => _brushInfo
                };
            return _brushInfo;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }

    /// <summary>
    /// Maps a bool flag to a colored brush (orange when true, dark grey when false).
    /// Used for highlighting mixed worksets or other status flags.
    /// </summary>
    public class MixedWorksetToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush Mixed = new(Color.FromRgb(0xFF, 0x6B, 0x00));
        private static readonly SolidColorBrush Normal = new(Color.FromRgb(0x1C, 0x1C, 0x1E));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Mixed : Normal;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }
}