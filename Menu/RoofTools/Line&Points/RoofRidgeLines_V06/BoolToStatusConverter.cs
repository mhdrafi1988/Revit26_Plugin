// File: BoolToStatusConverter.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.Views.Converters

using System;
using System.Globalization;
using System.Windows.Data;

namespace Revit26_Plugin.RoofRidgeLines_V06.Views.Converters
{
    public class BoolToStatusConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return b ? "✔ Valid" : "✖ Invalid";

            return "Unknown";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
