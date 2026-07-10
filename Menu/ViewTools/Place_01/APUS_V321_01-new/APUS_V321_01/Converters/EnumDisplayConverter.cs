// File: Converters/EnumDisplayConverter.cs
using Revit26_Plugin.APUS_V321_01.Models;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Revit26_Plugin.APUS_V321_01.Converters
{
    /// <summary>
    /// Renders enum values used in the Reading Order card dropdowns as
    /// friendly display strings instead of raw enum names.
    /// </summary>
    public class EnumDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                ReadingDirection.TopToBottom_LeftToRight => "Top \u2192 Bottom, Left \u2192 Right",
                ReadingDirection.TopToBottom_RightToLeft => "Top \u2192 Bottom, Right \u2192 Left",
                ReadingDirection.BottomToTop_LeftToRight => "Bottom \u2192 Top, Left \u2192 Right",
                ReadingDirection.BottomToTop_RightToLeft => "Bottom \u2192 Top, Right \u2192 Left",

                RowTiebreak.XPosition => "X position",
                RowTiebreak.Name => "Name",
                RowTiebreak.Size => "Size",

                MarkerSource.SectionCurveMidpoint => "Section curve mid",
                MarkerSource.CropBoxCenter => "Crop box center",
                MarkerSource.ViewOrigin => "View origin",

                SortFallback.PushToEnd => "Push to end",
                SortFallback.SortByName => "Sort by name",

                _ => value?.ToString() ?? string.Empty
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }

    /// <summary>
    /// Shows a small up/down arrow next to the active sort column header,
    /// nothing on inactive columns. Parameter is the SectionGridSortColumn
    /// this header represents; binds to the ViewModel's SortColumn.
    /// </summary>
    public class SortArrowConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 2 || values[0] == null || values[1] == null)
                return string.Empty;

            var activeColumn = (SectionGridSortColumn)values[0];
            var thisColumn = (SectionGridSortColumn)values[1];
            var ascending = values.Length > 2 && values[2] is bool b && b;

            if (activeColumn != thisColumn)
                return string.Empty;

            return ascending ? "\u2191" : "\u2193";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}
