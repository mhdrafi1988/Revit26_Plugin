using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.V005.Helpers
{
    /// <summary>
    /// Converts an int count to Visibility, inverse of the shared
    /// IntToVisibilityConverter (which shows on non-zero) — this one shows
    /// only when the count IS zero. Used for the "no drain points picked"
    /// warning message, which should only appear when PickedPoints.Count == 0.
    /// Kept local rather than added to shared Converters.cs since the inverse
    /// polarity is specific to this one display case.
    /// </summary>
    public class ZeroCountToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => (value is int i && i == 0) ? Visibility.Visible : Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
