using System;
using System.Globalization;
using System.Windows.Data;

namespace Revit26_Plugin.DwgToDetailLines.V010.Helpers
{
    public class BooleanToEnumConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value?.ToString() == p?.ToString();

        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => (bool)value ? Enum.Parse(t, p.ToString()) : Binding.DoNothing;
    }

    /// <summary>
    /// Displays a nullable int metric as its number, or an em dash when null
    /// (pre-conversion state). Used directly on TextBlock.Text — replaces the
    /// previous Style.Setter + DataTrigger Value="{x:Null}" pattern in the
    /// Metrics Card, which did not reliably re-evaluate when the whole
    /// ConversionMetrics instance was swapped after Convert.
    /// </summary>
    public class NullableIntToDashConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is int i ? i.ToString(c) : "—";

        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => Binding.DoNothing;
    }

    /// <summary>
    /// Metric-count foreground: gray when null (not yet run), navy when zero,
    /// and the supplied "attention" brush key (via ConverterParameter) when
    /// greater than zero. ConverterParameter must be a StaticResource key name
    /// resolvable from the bound element's resources (e.g. "BrushWarning",
    /// "BrushDanger") — resolved against Application/host resources at convert
    /// time since IValueConverter has no direct FrameworkElement context.
    /// </summary>
    public class MetricCountToBrushConverter : IValueConverter
    {
        private static readonly System.Windows.Media.SolidColorBrush Gray =
            new(System.Windows.Media.Color.FromRgb(0xAE, 0xB6, 0xBF));
        private static readonly System.Windows.Media.SolidColorBrush Navy =
            new(System.Windows.Media.Color.FromRgb(0x1E, 0x3A, 0x5F));
        private static readonly System.Windows.Media.SolidColorBrush Warning =
            new(System.Windows.Media.Color.FromRgb(0xB4, 0x53, 0x09));
        private static readonly System.Windows.Media.SolidColorBrush Danger =
            new(System.Windows.Media.Color.FromRgb(0xD9, 0x53, 0x4F));

        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            if (value is not int i)
                return Gray;

            if (i == 0)
                return Navy;

            return p as string == "Danger" ? Danger : Warning;
        }

        public object ConvertBack(object value, Type t, object p, CultureInfo c)
            => Binding.DoNothing;
    }
}
