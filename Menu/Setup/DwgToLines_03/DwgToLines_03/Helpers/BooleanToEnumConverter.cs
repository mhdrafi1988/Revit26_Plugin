using System;
using System.Globalization;
using System.Windows.Data;

namespace Revit26_Plugin.DwgSymbolicConverter_V03.Helpers
{
    public class BooleanToEnumConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value?.ToString() == p?.ToString();

        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => (bool)value ? Enum.Parse(t, p.ToString()) : Binding.DoNothing;
    }
}
