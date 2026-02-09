using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Revit26_Plugin.APUS_V312.ViewModels;

namespace Revit26_Plugin.APUS_V312.Views
{
    /// <summary>
    /// Inverts a boolean value (true becomes false, false becomes true)
    /// </summary>
    public class BooleanToNotConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b)
                return !b;
            return value;
        }
    }

    /// <summary>
    /// Converts boolean to Visibility (Visible/Collapsed)
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                bool result = Invert ? !boolValue : boolValue;
                return result ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                bool result = visibility == Visibility.Visible;
                return Invert ? !result : result;
            }
            return false;
        }
    }

    /// <summary>
    /// Converts LogLevel enum to Brush color for UI display
    /// </summary>
    public class LogLevelToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is LogLevel level)
            {
                return level switch
                {
                    LogLevel.Info => Brushes.Black,
                    LogLevel.Warning => Brushes.Orange,
                    LogLevel.Error => Brushes.Red,
                    _ => Brushes.Black
                };
            }
            return Brushes.Black;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}