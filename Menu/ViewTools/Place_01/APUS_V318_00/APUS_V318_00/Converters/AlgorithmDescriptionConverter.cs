// File: AlgorithmDescriptionConverter.cs
using Revit26_Plugin.APUS_V318.Models;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Revit26_Plugin.APUS_V318.Converters
{
    public class AlgorithmDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is PlacementAlgorithm algorithm)
            {
                return algorithm switch
                {
                    PlacementAlgorithm.Grid => "Uniform grid layout with fixed columns. Best for consistent view sizes.",

                    PlacementAlgorithm.BinPacking => "Space-optimized packing across multiple sheets. Maximizes sheet usage efficiency.",

                    PlacementAlgorithm.ReadingOrder => "Strict left-to-right, top-to-bottom placement across multiple sheets. Maintains spatial order.",

                    PlacementAlgorithm.AdaptiveGrid => "Adaptive grid based on view sizes. Groups similar sizes together.",

                    PlacementAlgorithm.ReadingOrderBinPacking => "Reading order sorting with single-sheet bin packing. Stops immediately when sheet is full.",

                    PlacementAlgorithm.MultiSheetOptimizer => "🎯 SMART SHEET OPTIMIZER: Places maximum views on ONE sheet with 95% utilization target.\n\n" +
                                                              "✓ No overlapping sections\n" +
                                                              "✓ Keeps user gaps (±10% tolerance)\n" +
                                                              "✓ Bottom-aligned per row\n" +
                                                              "✓ Left-aligned within rows\n" +
                                                              "✓ TARGET: 95% sheet utilization\n" +
                                                              "✓ If <95%, tries up to 3 attempts to improve\n" +
                                                              "✓ Tests 3 gap variations per attempt\n" +
                                                              "✓ ONE SHEET ONLY - skips remaining sections\n" +
                                                              "✓ Live UI updates with detailed progress\n\n" +
                                                              "The algorithm will keep trying different gap combinations until it either reaches 95% utilization or runs out of attempts with no meaningful improvement.",

                    _ => "Select a placement algorithm"
                };
            }

            return "Select a placement algorithm";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}