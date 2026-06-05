// File: Converters/PluginStateConverter.cs
using Revit26_Plugin.APUS_V330.ViewModels;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Revit26_Plugin.APUS_V330.Converters
{
    public class PluginStateConverter : IMultiValueConverter, IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PluginState state)
            {
                string param = parameter as string;
                return param switch
                {
                    "IsEnabled" =>
                        state == PluginState.Idle         ||
                        state == PluginState.ReadyToPlace ||
                        state == PluginState.Completed    ||
                        state == PluginState.Error,

                    "IsProcessing" =>
                        state == PluginState.Processing ||
                        state == PluginState.Cancelling,

                    "PlaceButtonEnabled" =>
                        state == PluginState.ReadyToPlace,

                    "RefreshButtonEnabled" =>
                        state == PluginState.Idle         ||
                        state == PluginState.ReadyToPlace ||
                        state == PluginState.Completed    ||
                        state == PluginState.Error,

                    "ShowProgress" =>
                        (state == PluginState.Processing || state == PluginState.Cancelling)
                            ? Visibility.Visible : Visibility.Collapsed,

                    "ShowCancel" =>
                        state == PluginState.Processing
                            ? Visibility.Visible : Visibility.Collapsed,

                    "StatusColor" => state switch
                    {
                        PluginState.Idle         => Brushes.LightGray,
                        PluginState.ReadyToPlace => Brushes.LightBlue,
                        PluginState.Processing   => Brushes.LightGreen,
                        PluginState.Cancelling   => Brushes.Orange,
                        PluginState.Completed    => Brushes.Green,
                        PluginState.Error        => Brushes.Red,
                        _                        => Brushes.Gray
                    },

                    "StatusDotColor" => state switch
                    {
                        PluginState.Idle         => Brushes.Gray,
                        PluginState.ReadyToPlace => Brushes.Blue,
                        PluginState.Processing   => Brushes.Green,
                        PluginState.Cancelling   => Brushes.Orange,
                        PluginState.Completed    => Brushes.DarkGreen,
                        PluginState.Error        => Brushes.DarkRed,
                        _                        => Brushes.Black
                    },

                    "StatusText" => state.ToString(),

                    _ => DependencyProperty.UnsetValue
                };
            }
            return DependencyProperty.UnsetValue;
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
            => Convert(values[0], targetType, parameter, culture);

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
