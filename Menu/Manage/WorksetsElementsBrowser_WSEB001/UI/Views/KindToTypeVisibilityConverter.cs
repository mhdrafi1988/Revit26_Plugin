using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Revit26_Plugin.WorksetsElementsBrowser.WSEB001.UI.ViewModels;

namespace Revit26_Plugin.WorksetsElementsBrowser.WSEB001.UI.Views
{
    /// <summary>Shows the per-row "Show in 3D" button only on Type rows — Workset/Category rows have no single element set to show.</summary>
    public class KindToTypeVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is ElementTreeNodeKind.Type ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
