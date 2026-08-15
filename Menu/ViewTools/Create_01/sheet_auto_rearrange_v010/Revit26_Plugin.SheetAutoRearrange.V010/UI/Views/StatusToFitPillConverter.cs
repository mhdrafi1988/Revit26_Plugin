using Revit26_Plugin.SheetAutoRearrange.V010.Core.Models;
using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Revit26_Plugin.SheetAutoRearrange.V010.UI.Views
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
    /// V007 NEW: maps ViewSizeCategory to the grid's inline tag text next to
    /// the view name (e.g. "TALL", "WIDE", "TALL+WIDE"). Normal returns
    /// empty string — paired with SizeCategoryToTagVisibilityConverter to
    /// collapse the tag Border entirely rather than show an empty pill.
    /// </summary>
    public class SizeCategoryToTagTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is ViewSizeCategory cat
                ? cat switch
                {
                    ViewSizeCategory.Tall => "TALL",
                    ViewSizeCategory.Wide => "WIDE",
                    ViewSizeCategory.TallAndWide => "TALL+WIDE",
                    _ => string.Empty
                }
                : string.Empty;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>Collapses the tag Border for ViewSizeCategory.Normal so no empty pill renders.</summary>
    public class SizeCategoryToTagVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is ViewSizeCategory cat && cat != ViewSizeCategory.Normal
                ? System.Windows.Visibility.Visible
                : System.Windows.Visibility.Collapsed;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>Maps ViewSizeCategory to the tag pill's background brush — navy for Tall, purple for Wide, a blend for TallAndWide.</summary>
    public class SizeCategoryToTagBrushConverter : IValueConverter
    {
        private readonly SolidColorBrush _tall = new(Color.FromRgb(0x1E, 0x3A, 0x5F));       // BrushNavyPrimary
        private readonly SolidColorBrush _wide = new(Color.FromRgb(0x8B, 0x5C, 0xF6));       // purple, matches mockup
        private readonly SolidColorBrush _both = new(Color.FromRgb(0x2D, 0x6C, 0xDF));       // BrushAppleBlue

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is ViewSizeCategory cat
                ? cat switch
                {
                    ViewSizeCategory.Tall => _tall,
                    ViewSizeCategory.Wide => _wide,
                    ViewSizeCategory.TallAndWide => _both,
                    _ => _tall
                }
                : _tall;

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
}
