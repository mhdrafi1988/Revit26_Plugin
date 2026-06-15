using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Revit26_Plugin.APUS_V315.Views.Converters;

[ValueConversion(typeof(bool), typeof(Brush))]
public class BoolToColorConverter : IValueConverter
{
    public Color TrueColor { get; set; } = Colors.Green;
    public Color FalseColor { get; set; } = Colors.Gray;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return new SolidColorBrush(boolValue ? TrueColor : FalseColor);
        }

        return new SolidColorBrush(FalseColor);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}