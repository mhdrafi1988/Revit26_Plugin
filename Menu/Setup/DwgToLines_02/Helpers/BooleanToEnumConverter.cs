using System;
using System.Globalization;
using System.Windows.Data;

namespace Revit26_Plugin.DwgSymbolicConverter_V02.Helpers
{
    /// <summary>
    /// Allows RadioButtons to bind to an enum property.
    /// ConverterParameter = enum name (string).
    /// </summary>
    public sealed class BooleanToEnumConverter : IValueConverter
    {
        public static BooleanToEnumConverter Instance { get; } = new BooleanToEnumConverter();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null) return false;
            return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (parameter == null) return Binding.DoNothing;
            if (value is bool b && b)
                return Enum.Parse(targetType, parameter.ToString(), ignoreCase: true);

            return Binding.DoNothing;
        }
    }
}
