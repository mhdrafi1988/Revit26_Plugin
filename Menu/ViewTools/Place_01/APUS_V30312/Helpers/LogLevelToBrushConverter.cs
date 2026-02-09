using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Revit26_Plugin.APUS_V312.ViewModels;

namespace Revit26_Plugin.APUS_V312.Views
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
                    LogLevel.Warning => Brushes.Orange,
                    LogLevel.Error => Brushes.Red,
                    _ => Brushes.Black
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