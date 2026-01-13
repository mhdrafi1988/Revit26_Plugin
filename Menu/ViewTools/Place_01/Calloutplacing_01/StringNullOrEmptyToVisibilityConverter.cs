using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Revit22_Plugin.callout.Commands;
using Revit22_Plugin.callout.Helpers;
using Revit22_Plugin.callout.Models;

namespace Revit22_Plugin.callout.Converters
{
    public class StringNullOrEmptyToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
