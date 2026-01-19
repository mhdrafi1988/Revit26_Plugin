using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V306.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V306.Services
{
    /// <summary>
    /// FAIL-SAFE sorting for section views.
    /// Reading order:
    /// 1) Y first (Top ? Bottom) using Y-banding
    /// 2) X second (Left ? Right)
    /// 3) Stable fallback (ElementId)
    /// </summary>
    public static class SectionSortingService
    {
        // ?300 mm in feet
        private const double Y_TOLERANCE_FT = 1.0;

        public static List<SectionItemViewModel> Sort(
            IEnumerable<SectionItemViewModel> items)
        {
            var data = items
                .Select(i =>
                {
                    XYZ p = GetMarkerPoint(i.View);
                    return new SortNode
                    {
                        Item = i,
                        X = p.X,
                        Y = p.Y,
                        Id = (int)i.View.Id.Value   // ? FIX HERE
                    };
                })
                .ToList();

            if (data.Count == 0)
                return new List<SectionItemViewModel>();

            // STEP 1: Sort by Y descending (top first)
            data.Sort((a, b) => b.Y.CompareTo(a.Y));

            // STEP 2: Assign row indices using Y tolerance
            int currentRow = 0;
            double rowAnchorY = data[0].Y;

            data[0].Row = currentRow;

            for (int i = 1; i < data.Count; i++)
            {
                if (Math.Abs(data[i].Y - rowAnchorY) > Y_TOLERANCE_FT)
                {
                    currentRow++;
                    rowAnchorY = data[i].Y;
                }

                data[i].Row = currentRow;
            }

            // STEP 3: Final deterministic sort
            return data
                .OrderBy(n => n.Row)   // TOP ? BOTTOM
                .ThenBy(n => n.X)      // LEFT ? RIGHT
                .ThenBy(n => n.Id)     // FAIL-SAFE
                .Select(n => n.Item)
                .ToList();
        }

        private static XYZ GetMarkerPoint(ViewSection view)
        {
            if (view.Location is LocationCurve lc)
                return lc.Curve.Evaluate(0.5, true);

            BoundingBoxXYZ bb = view.CropBox;
            return (bb.Min + bb.Max) * 0.5;
        }

        private class SortNode
        {
            public SectionItemViewModel Item;
            public double X;
            public double Y;
            public int Row;
            public int Id;
        }
    }
}
