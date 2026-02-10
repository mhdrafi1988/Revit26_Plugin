using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V311.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V311.Services
{
    /// <summary>
    /// Sorts section views top ? bottom, then left ? right
    /// using a configurable Y tolerance (mm).
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
            View referenceView,
            double yToleranceMm)
        {
            if (referenceView == null)
                return new List<SortedSection>();

            XYZ origin = referenceView.Origin;
            XYZ right = referenceView.RightDirection;
            XYZ up = referenceView.UpDirection;

            double tol = Math.Max(yToleranceMm, 1.0);

            return items
                .Select(item =>
                {
                    XYZ marker = GetMarkerPoint(item.View);
                    XYZ v = marker - origin;

                    double rawX = v.DotProduct(right);
                    double rawY = v.DotProduct(up);

                    // Snap Y into tolerance buckets
                    double yBucket = Math.Round(rawY / tol) * tol;

                    return new SortedSection(item, rawX, yBucket);
                })
                .OrderByDescending(p => p.Y) // top ? bottom
                .ThenBy(p => p.X)             // left ? right
                .ToList();
        }

        private static XYZ GetMarkerPoint(ViewSection view)
        {
            if (view.Location is LocationCurve lc)
                return lc.Curve.Evaluate(0.5, true);

            BoundingBoxXYZ bb = view.CropBox;
            return (bb.Min + bb.Max) * 0.5;
        }
    }
}
