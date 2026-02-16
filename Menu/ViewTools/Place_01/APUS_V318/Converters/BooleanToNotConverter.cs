// File: BooleanToNotConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace Revit26_Plugin.APUS_V318.Converters
{
    public class BooleanToNotConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;

            return true; // Default to true if not boolean
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
                return !boolValue;

            return false;
        }
    }
}