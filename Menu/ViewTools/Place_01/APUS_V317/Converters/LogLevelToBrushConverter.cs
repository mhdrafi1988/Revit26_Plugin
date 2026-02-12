// File: LogLevelToBrushConverter.cs
using Revit26_Plugin.APUS_V317.ViewModels;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Revit26_Plugin.APUS_V317.Converters
{
    public class LogLevelToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogLevel level)
            {
                return level switch
                {
                    LogLevel.Info => Brushes.Black,
                    LogLevel.Warning => Brushes.DarkOrange,
                    LogLevel.Error => Brushes.Red,
                    LogLevel.Success => Brushes.Green,
                    _ => Brushes.Gray
                };
            }

            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}