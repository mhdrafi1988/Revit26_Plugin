using Revit26_Plugin.SheetAutoRearrange.V021.Core.Models;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Revit26_Plugin.SheetAutoRearrange.V021.UI.Views
{
    /// <summary>
    /// Tool-local converter (not merged to shared Converters.cs) mapping
    /// ViewFitStatus to the Fit column's display text. Mirrors
    /// RoofEdgeSections' StatusToEnabledConverter pattern — instantiated
    /// only in this Window's own Resources block.
    /// </summary>
    public class StatusToFitTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is ViewFitStatus status
                ? status switch
                {
                    ViewFitStatus.Fits => "OK",
                    ViewFitStatus.Overflow => "Overflow",
                    ViewFitStatus.NotSelected => "Not Selected",
                    _ => "—"
                }
                : "—";

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>Maps ViewFitStatus to the pill's brush — green/red/muted, matching SharedStyles status colors.</summary>
    public class StatusToFitBrushConverter : IValueConverter
    {
        private readonly SolidColorBrush _ok = new(Color.FromRgb(0x2E, 0x9E, 0x64));       // ColorSuccess
        private readonly SolidColorBrush _overflow = new(Color.FromRgb(0xD9, 0x53, 0x4F)); // ColorDanger
        private readonly SolidColorBrush _muted = new(Color.FromRgb(0x8F, 0xA3, 0xB8));    // TextTertiary

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is ViewFitStatus status
                ? status switch
                {
                    ViewFitStatus.Fits => _ok,
                    ViewFitStatus.Overflow => _overflow,
                    _ => _muted
                }
                : _muted;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// V008 NEW: collapses an element when the bound string is null/empty —
    /// used to hide the copiable log-path TextBox until a log has actually
    /// been saved once.
    /// </summary>
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is string s && !string.IsNullOrWhiteSpace(s)
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// V014 NEW: generic enum-to-bool comparer for the Row Fill Strategy
    /// segmented picker — binds each ToggleButton's IsChecked to
    /// "RowFillStrategy == (the enum value named in ConverterParameter)"
    /// without a dedicated bool property per strategy. One-way only (the
    /// actual strategy change happens in code-behind via
    /// StrategyButton_Click, not through this converter's ConvertBack) —
    /// matches the XAML binding's Mode=OneWay.
    /// </summary>
    public class EnumEqualsConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || parameter == null)
                return false;

            string? valueName = value.ToString();
            string? paramName = parameter.ToString();
            return string.Equals(valueName, paramName, StringComparison.Ordinal);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException("One-way only — see StrategyButton_Click in SheetAutoRearrangeWindow.xaml.cs for the actual strategy change.");
    }
}
