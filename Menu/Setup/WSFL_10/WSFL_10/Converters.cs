using Revit26_Plugin.WSFL_010.Models;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Revit26_Plugin.WSFL_010.Views
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

    /// <summary>Collapses a row when its integer value is zero.</summary>
    public class IntToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is int i && i == 0) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }

    /// <summary>
    /// Maps a LogLevel to the foreground color used in the dark log panel,
    /// matching the LogSuccess / LogWarning / LogError tokens from SharedStyles.
    /// </summary>
    public class LogLevelToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush BrushInfo    = new(Color.FromRgb(0x64, 0xD2, 0xFF)); // #64D2FF
        private static readonly SolidColorBrush BrushWarning = new(Color.FromRgb(0xFF, 0x9F, 0x0A)); // #FF9F0A
        private static readonly SolidColorBrush BrushError   = new(Color.FromRgb(0xFF, 0x45, 0x3A)); // #FF453A
        private static readonly SolidColorBrush BrushSuccess = new(Color.FromRgb(0x34, 0xC7, 0x59)); // #34C759

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogLevel level)
            {
                return level switch
                {
                    LogLevel.Warning => BrushWarning,
                    LogLevel.Error   => BrushError,
                    _                => BrushInfo
                };
            }
            return BrushSuccess;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }

    /// <summary>Shows an orange foreground when IsMixedWorkset is true.</summary>
    public class MixedWorksetToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush Mixed  = new(Color.FromRgb(0xFF, 0x6B, 0x00)); // OrangeRed
        private static readonly SolidColorBrush Normal = new(Color.FromRgb(0x1C, 0x1C, 0x1E)); // TextPrimary

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is bool b && b) ? Mixed : Normal;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => DependencyProperty.UnsetValue;
    }
}
