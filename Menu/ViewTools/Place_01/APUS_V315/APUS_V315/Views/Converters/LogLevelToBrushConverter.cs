using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Revit26_Plugin.APUS_V315.Models.Enums;

namespace Revit26_Plugin.APUS_V315.Views.Converters;

[ValueConversion(typeof(LogLevel), typeof(Brush))]
public class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is LogLevel level)
        {
            return level switch
            {
                LogLevel.Info => new SolidColorBrush(Colors.Black),
                LogLevel.Warning => new SolidColorBrush(Colors.DarkOrange),
                LogLevel.Error => new SolidColorBrush(Colors.Red),
                LogLevel.Success => new SolidColorBrush(Colors.Green),
                _ => new SolidColorBrush(Colors.Gray)
            };
        }

        return new SolidColorBrush(Colors.Black);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}