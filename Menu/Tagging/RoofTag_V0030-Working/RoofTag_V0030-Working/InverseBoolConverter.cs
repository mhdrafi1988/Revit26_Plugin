using System;
using System.Globalization;
using System.Windows.Data;

namespace Revit22_Plugin.RoofTagV3
{
    public class InverseBoolConverter_V3 : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return value;
        }
    }
}