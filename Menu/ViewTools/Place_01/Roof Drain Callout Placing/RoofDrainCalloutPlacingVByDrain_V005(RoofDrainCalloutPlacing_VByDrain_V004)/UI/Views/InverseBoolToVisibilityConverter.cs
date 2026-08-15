using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Revit26_Plugin.RoofDrainCalloutPlacingVByDrain.V005.Views
{
    /// <summary>
    /// Converts bool to the inverse Visibility (true → Collapsed, false → Visible).
    /// Used to show the Fixed-sizing detail panel only when IsAutoMode is false,
    /// alongside SharedStyles' standard BooleanToVisibilityConverter for the
    /// Auto-mode panel (true → Visible).
    /// Note: per project convention, converters are instantiated locally per
    /// Window/UserControl (never registered in the shared ResourceDictionary).
    /// </summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool b = value is bool bb && bb;
            return b ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException("InverseBoolToVisibilityConverter is one-way only.");
        }
    }

    /// <summary>
    /// Plain bool inverter (true → false, false → true). Used for the "Fixed"
    /// toggle button's IsChecked, which should be the logical NOT of IsAutoMode.
    /// Distinct from InverseBoolToVisibilityConverter above, which targets
    /// Visibility rather than bool.
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && !b;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && !b;
        }
    }
}
