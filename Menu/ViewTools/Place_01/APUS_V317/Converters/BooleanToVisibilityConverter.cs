// File: BooleanToVisibilityConverter.cs
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace Revit26_Plugin.APUS_V317.Converters
{
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }
        public bool UseHidden { get; set; }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                bool result = Invert ? !boolValue : boolValue;
                return result
                    ? Visibility.Visible
                    : (UseHidden ? Visibility.Hidden : Visibility.Collapsed);
            }

            return UseHidden ? Visibility.Hidden : Visibility.Collapsed;
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
}