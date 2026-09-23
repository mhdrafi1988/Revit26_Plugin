using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Revit26_Plugin.WorksetsElementsBrowser.WSEB001.UI.Views
{
    /// <summary>Non-empty string -> Visible, empty/null -> Collapsed. Used to hide the owner/editable-badge cells when there's nothing to show (category/type rows).</summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
