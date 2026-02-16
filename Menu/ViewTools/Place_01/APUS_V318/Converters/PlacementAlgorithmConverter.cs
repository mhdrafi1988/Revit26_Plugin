// File: PlacementAlgorithmConverter.cs
using Revit26_Plugin.APUS_V318.Models;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Revit26_Plugin.APUS_V318.Converters
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
                    PlacementAlgorithm.ReadingOrder => "Reading Order",
                    PlacementAlgorithm.AdaptiveGrid => "Adaptive Grid",
                    PlacementAlgorithm.ReadingOrderBinPacking => "Reading Order + Bin",
                    PlacementAlgorithm.MultiSheetOptimizer => "🎯 Smart Sheet Optimizer (95% Target)",
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
                    "Reading Order" => PlacementAlgorithm.ReadingOrder,
                    "Adaptive Grid" => PlacementAlgorithm.AdaptiveGrid,
                    "Reading Order + Bin" => PlacementAlgorithm.ReadingOrderBinPacking,
                    "🎯 Smart Sheet Optimizer (95% Target)" => PlacementAlgorithm.MultiSheetOptimizer,
                    _ => PlacementAlgorithm.Grid
                };
            }

            return PlacementAlgorithm.Grid;
        }
    }
}