// ==============================================
// File: LineStyleMatcher.cs
// Layer: Core/Services
// ADDED in V014: picks, for each CAD layer, the closest line style (or,
// for hatch layers, filled region type) from the user's shortlist, so a
// DWG with dozens of layers lands on a handful of existing styles instead
// of one new style per layer.
// ==============================================

using System;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.DwgToDetailLines.V014.Core.Models;

namespace Revit26_Plugin.DwgToDetailLines.V014.Core.Services
{
    /// <summary>
    /// Pairs CAD layers with shortlisted line styles. An exact (case-insensitive)
    /// name match wins; otherwise the style with the lowest weighted difference in
    /// colour (60%), lineweight (25%) and solid-vs-patterned (15%) is chosen —
    /// or by colour alone for hatches.
    /// </summary>
    public static class LineStyleMatcher
    {
        private const double ColourWeight = 0.60;
        private const double LineWeightWeight = 0.25;
        private const double PatternWeight = 0.15;
        private const double MaxRgbDistance = 441.6729559; // sqrt(3 * 255²)

        /// <summary>
        /// Returns the shortlisted style name that best matches <paramref name="layerName"/>,
        /// or null when <paramref name="shortlist"/> is empty.
        /// </summary>
        /// <param name="layerName">CAD layer name.</param>
        /// <param name="layer">CAD layer appearance, or null when unknown (first shortlisted style is used).</param>
        /// <param name="shortlist">Shortlisted styles / types, in display order.</param>
        /// <param name="colourOnly">Compare colour only (hatch layers → filled region types).</param>
        public static string BestMatch(
            string layerName,
            CadLayerAppearance layer,
            IReadOnlyList<StyleOption> shortlist,
            bool colourOnly = false)
        {
            if (shortlist == null || shortlist.Count == 0)
                return null;

            var byName = shortlist.FirstOrDefault(s =>
                string.Equals(s.Name, layerName, StringComparison.OrdinalIgnoreCase));
            if (byName != null)
                return byName.Name;

            if (layer == null)
                return shortlist[0].Name;

            return shortlist
                .Where(s => s.Appearance != null)
                .OrderBy(s => colourOnly ? ColourDistance(layer, s.Appearance) : Score(layer, s.Appearance))
                .Select(s => s.Name)
                .FirstOrDefault() ?? shortlist[0].Name;
        }

        /// <summary>Weighted difference between two appearances: 0 = identical, 1 = opposite.</summary>
        public static double Score(CadLayerAppearance a, CadLayerAppearance b)
        {
            double colour = ColourDistance(a, b);

            double weight = a.LineWeight > 0 && b.LineWeight > 0
                ? Math.Abs(a.LineWeight - b.LineWeight) / 15.0
                : 0.5;

            double pattern = a.IsSolid == b.IsSolid ? 0 : 1;

            return ColourWeight * colour + LineWeightWeight * weight + PatternWeight * pattern;
        }

        /// <summary>RGB distance between two appearances, 0 (same) to 1 (black vs white).</summary>
        public static double ColourDistance(CadLayerAppearance a, CadLayerAppearance b)
        {
            double dr = a.R - b.R, dg = a.G - b.G, db = a.B - b.B;
            return Math.Sqrt(dr * dr + dg * dg + db * db) / MaxRgbDistance;
        }
    }
}
