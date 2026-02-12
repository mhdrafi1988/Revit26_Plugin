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
                    PlacementAlgorithm.BinPacking => "Space-optimized packing. Maximizes sheet usage efficiency.",
                    PlacementAlgorithm.ReadingOrder => "Strict left-to-right, top-to-bottom placement. Maintains spatial order.",
                    PlacementAlgorithm.AdaptiveGrid => "Adaptive grid based on view sizes. Groups similar sizes together.",
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