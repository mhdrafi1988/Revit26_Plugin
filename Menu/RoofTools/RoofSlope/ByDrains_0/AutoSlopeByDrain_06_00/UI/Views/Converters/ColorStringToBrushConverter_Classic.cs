using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Revit26_Plugin.AutoSlopeByDrain_06_00.UI.Views.Converters
{
    public class ColorStringToBrushConverter_Classic : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string colorString)
            {
                try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(colorString)); }
                catch { return Brushes.Black; }
            }
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}