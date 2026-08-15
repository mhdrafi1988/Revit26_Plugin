using Revit26_Plugin.SheetAutoRearrange.V008.Core.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SheetAutoRearrange.V008.Core.Services
{
    /// <summary>
    /// Computes the mode height and mode width across a ticked set of views,
    /// then classifies each view as Normal / Tall / Wide / TallAndWide based
    /// on TallWideDetectionSettings (independent per axis).
    ///
    /// ASSUMPTION (flagged, not explicitly specified by Rafi): real-world mm
    /// sizes essentially never repeat exactly, so an exact-match mode would
    /// almost always be empty/meaningless. Heights and widths are bucketed to
    /// the nearest 5mm before computing mode — common view sizes cluster into
    /// the same bucket even with minor variance. Exposed as a constant for
    /// easy tuning if 5mm proves too coarse/fine in practice.
    /// </summary>
    public class ViewSizeClassifierService
    {
        private const double BucketSizeMm = 5.0;

        public class ClassificationResult
        {
            public double ModeHeightMm { get; set; }
            public double ModeWidthMm { get; set; }
            public Dictionary<ViewOnSheetItem, ViewSizeCategory> Categories { get; } = new();
        }

        public ClassificationResult Classify(
            List<ViewOnSheetItem> tickedItems,
            TallWideDetectionSettings tallSettings,
            TallWideDetectionSettings wideSettings)
        {
            var result = new ClassificationResult();

            if (tickedItems.Count == 0)
                return result;

            result.ModeHeightMm = ComputeModeBucketed(tickedItems.Select(i => i.HeightMm));
            result.ModeWidthMm = ComputeModeBucketed(tickedItems.Select(i => i.WidthMm));

            double tallThresholdMin = 0, tallThresholdMax = double.MaxValue;
            if (tallSettings.IsEnabled && result.ModeHeightMm > 0)
            {
                double target = result.ModeHeightMm * tallSettings.Multiplier;
                double band = target * (tallSettings.TolerancePercent / 100.0);
                tallThresholdMin = target - band;
                tallThresholdMax = target + band;
            }

            double wideThresholdMin = 0, wideThresholdMax = double.MaxValue;
            if (wideSettings.IsEnabled && result.ModeWidthMm > 0)
            {
                double target = result.ModeWidthMm * wideSettings.Multiplier;
                double band = target * (wideSettings.TolerancePercent / 100.0);
                wideThresholdMin = target - band;
                wideThresholdMax = target + band;
            }

            foreach (var item in tickedItems)
            {
                bool isTall = tallSettings.IsEnabled
                    && item.HeightMm >= tallThresholdMin
                    && item.HeightMm <= tallThresholdMax;

                bool isWide = wideSettings.IsEnabled
                    && item.WidthMm >= wideThresholdMin
                    && item.WidthMm <= wideThresholdMax;

                result.Categories[item] = (isTall, isWide) switch
                {
                    (true, true) => ViewSizeCategory.TallAndWide,
                    (true, false) => ViewSizeCategory.Tall,
                    (false, true) => ViewSizeCategory.Wide,
                    _ => ViewSizeCategory.Normal
                };
            }

            return result;
        }

        /// <summary>Buckets each value to the nearest BucketSizeMm, then returns the most frequent bucket's representative (unrounded average of members) as the mode.</summary>
        private double ComputeModeBucketed(IEnumerable<double> valuesMm)
        {
            var list = valuesMm.ToList();
            if (list.Count == 0)
                return 0;

            var buckets = list
                .GroupBy(v => System.Math.Round(v / BucketSizeMm) * BucketSizeMm)
                .OrderByDescending(g => g.Count())
                .ThenBy(g => g.Key) // stable tie-break: smallest bucket wins on a count tie
                .ToList();

            var modeBucket = buckets[0];
            return modeBucket.Average(); // representative value = actual average of the members in the winning bucket, not the rounded bucket key itself
        }
    }
}
