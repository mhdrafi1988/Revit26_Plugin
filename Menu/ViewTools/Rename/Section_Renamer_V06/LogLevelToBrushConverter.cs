using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using Revit26_Plugin.SARV6.ViewModels;

namespace Revit26_Plugin.SARV6.Views.Converters;

public class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value switch
        {
            UiLogLevel.Info => Brushes.LightGray,
            UiLogLevel.Warning => Brushes.Orange,
            UiLogLevel.Error => Brushes.IndianRed,
            UiLogLevel.Success => Brushes.LightGreen,
            _ => Brushes.White
        };

    public object ConvertBack(object v, Type t, object p, CultureInfo c)
        => throw new NotSupportedException();
}
