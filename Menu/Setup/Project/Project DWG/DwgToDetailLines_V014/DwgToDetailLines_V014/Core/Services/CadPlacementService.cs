// ==============================================
// File: CadPlacementService.cs
// Layer: Core/Services
// ADDED in V014: "Place beside CAD" — converted elements are moved to the
// right of the CAD import by one bounding-box width, so the result sits
// next to the source instead of on top of it.
// ==============================================

using Autodesk.Revit.DB;
using System;

namespace Revit26_Plugin.DwgToDetailLines.V014.Core.Services
{
    /// <summary>Works out where converted elements go relative to the CAD import.</summary>
    public static class CadPlacementService
    {
        /// <summary>
        /// Offset that moves geometry one CAD bounding-box width to the right in
        /// <paramref name="view"/> (along its RightDirection). Returns null when the
        /// import has no usable bounding box.
        /// </summary>
        /// <param name="import">The CAD import or link.</param>
        /// <param name="view">The drafting view elements are created in.</param>
        /// <param name="width">Bounding-box width along the view's right direction, in feet.</param>
        public static XYZ GetBesideOffset(ImportInstance import, View view, out double width)
        {
            width = 0;
            if (import == null || view == null)
                return null;

            BoundingBoxXYZ box = import.get_BoundingBox(view) ?? import.get_BoundingBox(null);
            if (box == null || box.Min == null || box.Max == null)
                return null;

            XYZ right = view.RightDirection;
            Transform t = box.Transform ?? Transform.Identity;

            double min = double.MaxValue, max = double.MinValue;
            foreach (double x in new[] { box.Min.X, box.Max.X })
            foreach (double y in new[] { box.Min.Y, box.Max.Y })
            foreach (double z in new[] { box.Min.Z, box.Max.Z })
            {
                double d = t.OfPoint(new XYZ(x, y, z)).DotProduct(right);
                min = Math.Min(min, d);
                max = Math.Max(max, d);
            }

            width = max - min;
            if (width <= 1e-9)
                return null;

            return right.Multiply(width);
        }
    }
}
