using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V306.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V306.Services
{
    /// <summary>
    /// Sorts section views strictly Left ? Right
    /// using rounded projected X/Y coordinates (0.0 precision).
    /// </summary>
    public static class SectionSortingService
    {
        public class SortedSection
        {
            public SectionItemViewModel Item { get; }
            public double X { get; }
            public double Y { get; }

            public SortedSection(
                SectionItemViewModel item,
                double x,
                double y)
            {
                Item = item;
                X = x;
                Y = y;
            }
        }

        public static List<SortedSection> SortLeftToRight(
            IEnumerable<SectionItemViewModel> items,
            View referenceView)
        {
            if (referenceView == null)
                return new List<SortedSection>();

            XYZ origin = referenceView.Origin;
            XYZ right = referenceView.RightDirection;
            XYZ up = referenceView.UpDirection;

            return items
                .Select(item =>
                {
                    XYZ modelPoint = GetMarkerPoint(item.View);
                    XYZ v = modelPoint - origin;

                    // Project to view axes
                    double rawX = v.DotProduct(right);
                    double rawY = v.DotProduct(up);

                    // ?? ROUND TO 0.0 PRECISION
                    double x = Math.Round(rawX, 1);
                    double y = Math.Round(rawY, 1);

                    return new SortedSection(item, x, y);
                })
                // STRICT LEFT ? RIGHT
                .OrderBy(p => p.Y)
                // Stable secondary (top ? bottom if same X)
                .ThenByDescending(p => p.X)
                .ToList();
        }

        /// <summary>
        /// Gets a stable marker position for a section view.
        /// </summary>
        private static XYZ GetMarkerPoint(ViewSection view)
        {
            if (view.Location is LocationCurve lc)
            {
                return lc.Curve.Evaluate(0.5, true);
            }

            BoundingBoxXYZ bb = view.CropBox;
            return (bb.Min + bb.Max) * 0.5;
        }
    }
}
