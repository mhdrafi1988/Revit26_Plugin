using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Revit26_Plugin.Shared.Models
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v == Visibility.Visible;
    }

    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && !b) ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility v && v != Visibility.Visible;
    }

    public class IntToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is int i && i == 0) ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }

    /// <summary>INFO → cyan, WARNING → orange, ERROR → red, SUCCESS → green.</summary>
    public class LogLevelToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush BrushInfo    = new(Color.FromRgb(0x64, 0xD2, 0xFF));
        private static readonly SolidColorBrush BrushWarning = new(Color.FromRgb(0xFF, 0x9F, 0x0A));
        private static readonly SolidColorBrush BrushError   = new(Color.FromRgb(0xFF, 0x45, 0x3A));
        private static readonly SolidColorBrush BrushSuccess = new(Color.FromRgb(0x4C, 0xC1, 0x8A));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogLevel level)
                return level switch
                {
                    LogLevel.Warning => BrushWarning,
                    LogLevel.Error   => BrushError,
                    LogLevel.Success => BrushSuccess,
                    _                => BrushInfo
                };
            return BrushInfo;
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }

    /// <summary>Orange foreground when IsMixedWorkset is true.</summary>
    public class MixedWorksetToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush Mixed  = new(Color.FromRgb(0xFF, 0x6B, 0x00));
        private static readonly SolidColorBrush Normal = new(Color.FromRgb(0x1C, 0x1C, 0x1E));

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Mixed : Normal;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }
}
