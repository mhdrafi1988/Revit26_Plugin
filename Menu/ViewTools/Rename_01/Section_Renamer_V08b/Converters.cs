using Revit26_Plugin.SectionAutoRenamer._01.ViewModels;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Revit26_Plugin.SectionAutoRenamer._01.Views.Converters;

/// <summary>
/// Maps UiLogLevel → foreground brush for the log panel.
/// BoolToVisibilityConverter is NOT declared here — it is already provided
/// by SharedStyles.xaml as BoolToVisibility (BooleanToVisibilityConverter).
/// </summary>
public class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value switch
        {
            UiLogLevel.Info    => new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93)), // TextSecondary
            UiLogLevel.Warning => new SolidColorBrush(Color.FromRgb(0xFF, 0x9F, 0x0A)), // ColorWarning
            UiLogLevel.Error   => new SolidColorBrush(Color.FromRgb(0xFF, 0x45, 0x3A)), // ColorDanger
            UiLogLevel.Success => new SolidColorBrush(Color.FromRgb(0x34, 0xC7, 0x59)), // ColorSuccess
            _                  => Brushes.Gray
        };

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}

/// <summary>IsDuplicate true → Visible, false → Collapsed</summary>
public class DupToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}
