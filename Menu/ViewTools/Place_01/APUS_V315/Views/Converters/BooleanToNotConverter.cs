using System;
using System.Globalization;
using System.Windows.Data;

namespace Revit26_Plugin.APUS_V315.Views.Converters;

[ValueConversion(typeof(bool), typeof(bool))]
public class BooleanToNotConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool boolValue ? !boolValue : true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool boolValue ? !boolValue : false;
    }
}