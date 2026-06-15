using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Revit26_Plugin.APUS_V315.Models.Enums;

namespace Revit26_Plugin.APUS_V315.Views.Converters;

[ValueConversion(typeof(PluginState), typeof(object))]
public class PluginStateConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not PluginState state || parameter is not string param)
            return DependencyProperty.UnsetValue;

        return param switch
        {
            "StatusColor" => state switch
            {
                PluginState.Idle => new SolidColorBrush(Colors.LightGray),
                PluginState.Initializing => new SolidColorBrush(Colors.LightBlue),
                PluginState.ReadyToPlace => new SolidColorBrush(Colors.LightGreen),
                PluginState.Processing => new SolidColorBrush(Colors.MediumSeaGreen),
                PluginState.Cancelling => new SolidColorBrush(Colors.Orange),
                PluginState.Completed => new SolidColorBrush(Colors.ForestGreen),
                PluginState.Error => new SolidColorBrush(Colors.IndianRed),
                _ => new SolidColorBrush(Colors.Gray)
            },

            "StatusText" => state.ToString(),

            _ => DependencyProperty.UnsetValue
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}