using System;
using System.Globalization;
using System.Windows.Data;

namespace WorksetManager_01.Helpers
{
    /// <summary>
    /// Returns false when the bound value is true, and true when false.
    /// Used to disable the Scan button while scanning is in progress.
    /// </summary>
    [ValueConversion(typeof(bool), typeof(bool))]
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && !b;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => value is bool b && !b;
    }
}
