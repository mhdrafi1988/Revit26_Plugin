using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Revit26_Plugin.AutoSlope.V5_00.Infrastructure.Helpers.Converters
{
    public class ColorStringToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorString)
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(colorString);
                    return new SolidColorBrush(color);
                }
                catch
                {
                    return new SolidColorBrush(Colors.Black);
                }
            }
            return new SolidColorBrush(Colors.Black);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}