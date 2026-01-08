using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Revit26_Plugin.RRLPV4.Converters
{
    /// <summary>
    /// Converts a boolean to Visibility:
    /// True  → Visible
    /// False → Collapsed
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is Visibility vis && vis == Visibility.Visible;
    }

    /// <summary>
    /// Converts a boolean to a Brush:
    /// True → Orange
    /// False → Black
    /// </summary>
    public class BooleanToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && b ? Brushes.Orange : Brushes.Black;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
