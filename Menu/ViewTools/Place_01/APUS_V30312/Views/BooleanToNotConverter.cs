using System;
using System.Globalization;
using System.Windows.Data;

namespace Revit26_Plugin.APUS_V312.Views
{
    public class BooleanToNotConverterV2 : IValueConverter
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