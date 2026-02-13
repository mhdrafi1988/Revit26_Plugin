// File: SectionSortingService.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V317.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V317.Services
{
    public static class SectionSortingService
    {
        public class SortedSection
        {
            public SectionItemViewModel Item { get; }
            public double X { get; }
            public double Y { get; }
            public double YBucket { get; set; }

            public SortedSection(SectionItemViewModel item, double x, double y)
            {
                Item = item;
                X = x;
                Y = y;
                YBucket = y;
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

            double tol = Math.Max(yToleranceMm, 1.0) / 304.8; // Convert mm to feet

            // First pass: assign Y buckets
            var sections = items
                .Select(item =>
                {
                    XYZ marker = GetMarkerPoint(item.View);
                    XYZ v = marker - origin;

                    double rawX = v.DotProduct(right);
                    double rawY = v.DotProduct(up);
                    double yBucket = Math.Round(rawY / tol) * tol;

                    return new SortedSection(item, rawX, yBucket);
                })
                .ToList();

            // Group by Y bucket
            var groups = sections
                .GroupBy(s => s.YBucket)
                .OrderByDescending(g => g.Key) // Top to bottom
                .ToList();

            var result = new List<SortedSection>();

            foreach (var group in groups)
            {
                // WITHIN EACH ROW: Sort by HEIGHT DESCENDING (tallest first)
                var rowItems = group
                    .OrderByDescending(s => GetViewHeight(s.Item.View)) // Tallest first
                    .ThenBy(s => s.X) // Then left to right as tiebreaker
                    .ToList();

                result.AddRange(rowItems);
            }

            return result;
        }

        private static double GetViewHeight(ViewSection view)
        {
            try
            {
                var bb = view.CropBox;
                if (bb != null)
                {
                    return bb.Max.Y - bb.Min.Y;
                }
            }
            catch { }
            return 0;
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