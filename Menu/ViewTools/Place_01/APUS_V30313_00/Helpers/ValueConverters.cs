using Revit26_Plugin.APUS_V313.Enums;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace Revit26_Plugin.APUS_V313.Helpers
{
    public class BooleanToNotConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b ? !b : value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b ? !b : value;
        }
    }

    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility v && v == Visibility.Visible;
        }
    }

    public class UiStateToVisibilityConverter : IValueConverter
    {
        public UiState TargetState { get; set; } = UiState.Placing;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is UiState state)
            {
                return state == TargetState ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class UiStateToBooleanConverter : IValueConverter
    {
        public UiState TargetState { get; set; } = UiState.Placing;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is UiState state)
            {
                return state == TargetState;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ProgressStateToVisibilityConverter : IValueConverter
    {
        public ProgressState TargetState { get; set; } = ProgressState.Running;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ProgressState state)
            {
                return state == TargetState ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class LogLevelToBrushConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            // Handle both enum and string values
            if (value is LogLevel level)
            {
                return level switch
                {
                    LogLevel.Warning => Brushes.Orange,
                    LogLevel.Error => Brushes.Red,
                    _ => Brushes.Black
                };
            }

            // Fallback for string values (backward compatibility)
            string levelStr = value?.ToString();
            return levelStr switch
            {
                "Warning" or "1" => Brushes.Orange,
                "Error" or "2" => Brushes.Red,
                _ => Brushes.Black
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class PlacementFilterStateToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PlacementFilterState state)
            {
                return state switch
                {
                    PlacementFilterState.All => "All",
                    PlacementFilterState.PlacedOnly => "Placed Only",
                    PlacementFilterState.UnplacedOnly => "Unplaced Only",
                    _ => state.ToString()
                };
            }
            return value?.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return str switch
                {
                    "All" => PlacementFilterState.All,
                    "Placed Only" => PlacementFilterState.PlacedOnly,
                    "Unplaced Only" => PlacementFilterState.UnplacedOnly,
                    _ => PlacementFilterState.All
                };
            }
            return PlacementFilterState.All;
        }
    }
}