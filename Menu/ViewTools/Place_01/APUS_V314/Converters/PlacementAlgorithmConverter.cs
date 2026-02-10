// File: PlacementAlgorithmConverter.cs
using Revit26_Plugin.APUS_V314.Models;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Revit26_Plugin.APUS_V314.Converters
{
    public class PlacementAlgorithmConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PlacementAlgorithm algorithm)
            {
                return algorithm switch
                {
                    PlacementAlgorithm.Grid => "Grid",
                    PlacementAlgorithm.BinPacking => "Bin Packing",
                    PlacementAlgorithm.Ordered => "Ordered",
                    PlacementAlgorithm.AdaptiveGrid => "Adaptive Grid",
                    _ => algorithm.ToString()
                };
            }

            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return str switch
                {
                    "Grid" => PlacementAlgorithm.Grid,
                    "Bin Packing" => PlacementAlgorithm.BinPacking,
                    "Ordered" => PlacementAlgorithm.Ordered,
                    "Adaptive Grid" => PlacementAlgorithm.AdaptiveGrid,
                    _ => PlacementAlgorithm.Grid
                };
            }

            return PlacementAlgorithm.Grid;
        }
    }
}