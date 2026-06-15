using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Revit26_Plugin.RRLPV4.Converters
{
    /// <summary>
    /// Converts a boolean to Visibility:
    /// True  → Visible
    /// False → Collapsed
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isVisible = value is bool b && b;

            // Optional parameter to invert the logic
            if (parameter is string invert && invert.ToLower() == "true")
                isVisible = !isVisible;

            return isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility visibility && visibility == Visibility.Visible;
        }
    }

    /// <summary>
    /// Converts a boolean to a Brush:
    /// True → Green (Success)
    /// False → Red (Error/Failure)
    /// </summary>
    public class BooleanToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = value is bool b && b;

            // Use parameter to specify different color pairs
            if (parameter is string colors)
            {
                var colorParts = colors.Split(',');
                if (colorParts.Length == 2)
                {
                    var trueBrush = GetBrushFromString(colorParts[0].Trim());
                    var falseBrush = GetBrushFromString(colorParts[1].Trim());
                    return boolValue ? trueBrush : falseBrush;
                }
            }

            // Default colors
            return boolValue ? new SolidColorBrush(Colors.Green) : new SolidColorBrush(Colors.Red);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        private Brush GetBrushFromString(string colorName)
        {
            return colorName.ToLower() switch
            {
                "green" => new SolidColorBrush(Colors.Green),
                "red" => new SolidColorBrush(Colors.Red),
                "orange" => new SolidColorBrush(Colors.Orange),
                "blue" => new SolidColorBrush(Colors.Blue),
                "black" => new SolidColorBrush(Colors.Black),
                "white" => new SolidColorBrush(Colors.White),
                "gray" or "grey" => new SolidColorBrush(Colors.Gray),
                _ => new SolidColorBrush(Colors.Black) // Default
            };
        }
    }

    /// <summary>
    /// Converts a boolean to a string:
    /// True → "Yes"
    /// False → "No"
    /// </summary>
    public class BooleanToYesNoConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = value is bool b && b;

            if (parameter is string customText)
            {
                var textParts = customText.Split(',');
                if (textParts.Length == 2)
                {
                    return boolValue ? textParts[0] : textParts[1];
                }
            }

            return boolValue ? "Yes" : "No";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                if (parameter is string customText)
                {
                    var textParts = customText.Split(',');
                    if (textParts.Length == 2)
                    {
                        if (str == textParts[0]) return true;
                        if (str == textParts[1]) return false;
                    }
                }

                return str.Equals("Yes", StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }
    }

    /// <summary>
    /// Converts a double to a string with optional formatting
    /// </summary>
    public class DoubleToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double d)
            {
                string format = parameter as string ?? "F2";
                return d.ToString(format, culture);
            }
            return "0.00";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (double.TryParse(value as string, NumberStyles.Any, culture, out double result))
                return result;
            return 0.0;
        }
    }

    /// <summary>
    /// Converts a DateTime to a formatted string
    /// </summary>
    public class DateTimeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is DateTime dateTime)
            {
                string format = parameter as string ?? "yyyy-MM-dd HH:mm:ss";
                return dateTime.ToString(format, culture);
            }
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (DateTime.TryParse(value as string, culture, DateTimeStyles.None, out DateTime result))
                return result;
            return DateTime.MinValue;
        }
    }
}