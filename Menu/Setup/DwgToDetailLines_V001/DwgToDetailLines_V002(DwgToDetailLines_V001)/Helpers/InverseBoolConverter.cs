using System;
using System.Globalization;
using System.Windows.Data;

namespace Revit26_Plugin.DwgToDetailLines.V002.Helpers
{
    /// <summary>
    /// Inverts a boolean binding. Used to disable the Convert button
    /// while IsRunning is true (Run button disable-while-in-progress rule).
    /// </summary>
    public class InverseBoolConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is bool b ? !b : value;

        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => value is bool b ? !b : value;
    }
}
