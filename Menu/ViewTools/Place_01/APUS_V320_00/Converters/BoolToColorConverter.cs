// File: BoolToColorConverter.cs
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Revit26_Plugin.APUS_V320.Converters
{
    public class BoolToColorConverter : IValueConverter
    {
        public Brush TrueColor { get; set; } = Brushes.Green;
        public Brush FalseColor { get; set; } = Brushes.Gray;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                return boolValue ? TrueColor : FalseColor;
            }

            return FalseColor;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
