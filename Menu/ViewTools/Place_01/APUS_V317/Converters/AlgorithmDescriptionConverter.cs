// File: AlgorithmDescriptionConverter.cs
using Revit26_Plugin.APUS_V317.Models;
using System;
using System.Globalization;
using System.Windows.Data;

namespace Revit26_Plugin.APUS_V317.Converters
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

                    PlacementAlgorithm.MultiSheetOptimizer => "📚 MULTI-SHEET OPTIMIZER: Places views across multiple sheets with optimal layout.\n\n" +
                                                              "✓ No overlapping sections\n" +
                                                              "✓ Bottom-aligned per row\n" +
                                                              "✓ Left-aligned within rows\n" +
                                                              "✓ Gaps maintained with ±10% tolerance\n" +
                                                              "✓ Maximizes sheet space utilization\n" +
                                                              "✓ MULTI-SHEET - when sheet full, moves to next\n" +
                                                              "✓ Detailed live progress updates\n\n" +
                                                              "The algorithm sorts all views in reading order once, then packs them sheet by sheet, trying 3 gap variations per sheet to find the best layout.",

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