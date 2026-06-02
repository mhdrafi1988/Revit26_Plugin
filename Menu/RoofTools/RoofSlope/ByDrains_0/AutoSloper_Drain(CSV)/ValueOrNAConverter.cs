using System;
using System.Globalization;
using System.Windows.Data;

namespace Revit26_Plugin.Asd_19.Views
{
    public class ValueOrNAConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values == null || values.Length < 2 || values[0] == null || !(values[0] is bool))
                return "N/A";

            bool slopeApplied = (bool)values[0];
            if (!slopeApplied || values[1] == null)
                return "N/A";

            string format = parameter as string ?? "F2";
            if (values[1] is IFormattable formattable)
                return formattable.ToString(format, culture);

            return values[1].ToString();
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}   