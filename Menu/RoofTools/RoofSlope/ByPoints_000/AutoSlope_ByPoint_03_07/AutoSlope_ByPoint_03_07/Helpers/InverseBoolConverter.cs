using System;
using System.Globalization;
using System.Windows.Data;

namespace Revit26_Plugin.AutoSlopeByPoint_30_07.Helpers
{
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object v, Type t, object p, CultureInfo c)
            => !(bool)v;

        public object ConvertBack(object v, Type t, object p, CultureInfo c)
            => !(bool)v;
    }
}
