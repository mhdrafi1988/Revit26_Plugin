using System;
using System.Globalization;
using System.Windows.Data;

namespace Revit26_Plugin.AutoSlopeByPoint.V028.UI.Views
{
    /// <summary>
    /// Backs the editable Slope &gt; Percentage ComboBox. Formats the bound
    /// SlopePercent (double) as "1.5%" for display, and parses typed text
    /// (with or without a trailing "%") back to a double.
    ///
    /// Invalid or in-progress input (e.g. "1." while the user is still
    /// typing, or an empty box) returns Binding.DoNothing instead of
    /// pushing a bad value — the last valid SlopePercent is left in place
    /// until the user finishes typing a parseable number.
    /// </summary>
    public class SlopePercentTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
                return d.ToString("0.####", CultureInfo.CurrentCulture) + "%";
            return value?.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = (value as string ?? string.Empty).Trim().TrimEnd('%', ' ');
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var result))
                return result;
            return Binding.DoNothing;
        }
    }
}
