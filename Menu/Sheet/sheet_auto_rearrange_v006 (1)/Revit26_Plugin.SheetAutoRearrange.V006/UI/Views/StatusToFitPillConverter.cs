using Revit26_Plugin.SheetAutoRearrange.V006.Core.Models;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Revit26_Plugin.SheetAutoRearrange.V006.UI.Views
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
}
