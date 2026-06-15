// File: Converters/BooleanToNotConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;

namespace Revit26_Plugin.APUS_V330.Converters
{
    public class BooleanToNotConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : (object)true;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b ? !b : (object)false;
    }
}
