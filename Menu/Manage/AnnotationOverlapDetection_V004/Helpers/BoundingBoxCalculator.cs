using Autodesk.Revit.DB;
using Revit26_Plugin.AnnotationOverlapDetection.V004.Models;
using System;

namespace Revit26_Plugin.AnnotationOverlapDetection.V004.Helpers
{
    /// <summary>
    /// Pure geometry helpers. No Revit transactions, no UI references.
    /// All public methods work in mm; Revit internal units are feet, so
    /// conversion happens once, right when a value is read off the element.
    /// </summary>
    internal static class BoundingBoxCalculator
    {
        private const double FeetToMm = 304.8;

        /// <summary>
        /// Reads an element's bounding box (as seen in the given view) and returns it as
        /// (x, y, width, height) in mm, measured along the view's own right / up axes.
        /// This makes the check work in sections, elevations and drafting views, not only
        /// plans. In a plan view the axes are world X / Y, so results match v4.0.
        /// Returns null if the element has no bounding box in this view (e.g. hidden).
        /// </summary>
        public static (double x, double y, double width, double height)? GetBoundingBox(Element elem, View view)
        {
            BoundingBoxXYZ bbox = elem.get_BoundingBox(view);
            if (bbox == null)
                return null;

            XYZ right = view.RightDirection;
            XYZ up = view.UpDirection;
            Transform t = bbox.Transform ?? Transform.Identity;

            double minU = double.MaxValue, maxU = double.MinValue;
            double minV = double.MaxValue, maxV = double.MinValue;

            // Project all 8 corners onto the view plane axes.
            for (int i = 0; i < 8; i++)
            {
                var local = new XYZ(
                    (i & 1) == 0 ? bbox.Min.X : bbox.Max.X,
                    (i & 2) == 0 ? bbox.Min.Y : bbox.Max.Y,
                    (i & 4) == 0 ? bbox.Min.Z : bbox.Max.Z);
                XYZ p = t.OfPoint(local);

                double u = p.DotProduct(right);
                double v = p.DotProduct(up);
                minU = Math.Min(minU, u); maxU = Math.Max(maxU, u);
                minV = Math.Min(minV, v); maxV = Math.Max(maxV, v);
            }

            return (minU * FeetToMm, minV * FeetToMm, (maxU - minU) * FeetToMm, (maxV - minV) * FeetToMm);
        }

        /// <summary>
        /// Insertion point in mm - defaults to the bounding box min corner.
        /// </summary>
        public static (double x, double y) GetInsertionPoint(AnnotationData data)
        {
            return (data.X, data.Y);
        }

        /// <summary>
        /// Axis-aligned bounding box intersection test. Overlap area must be > 0,
        /// so boxes that merely touch at an edge do not count as overlapping.
        /// </summary>
        public static bool DoBoxesIntersect(AnnotationData a, AnnotationData b)
        {
            bool overlapsX = a.X < b.Right && b.X < a.Right;
            bool overlapsY = a.Y < b.Bottom && b.Y < a.Bottom;
            return overlapsX && overlapsY;
        }

        /// <summary>
        /// Width and height of the overlap area in model mm (0 when the boxes do not overlap).
        /// </summary>
        public static (double width, double height) GetOverlapSize(AnnotationData a, AnnotationData b)
        {
            double w = Math.Min(a.Right, b.Right) - Math.Max(a.X, b.X);
            double h = Math.Min(a.Bottom, b.Bottom) - Math.Max(a.Y, b.Y);
            return (Math.Max(0, w), Math.Max(0, h));
        }

        /// <summary>
        /// Vertical and horizontal gap between two boxes, in mm.
        /// 0 means the boxes touch/overlap on that axis; positive means separated.
        /// </summary>
        public static (double vGap, double hGap) CalculateGap(AnnotationData a, AnnotationData b)
        {
            double vGap = 0;
            if (a.Bottom <= b.Y)
                vGap = b.Y - a.Bottom;
            else if (b.Bottom <= a.Y)
                vGap = a.Y - b.Bottom;

            double hGap = 0;
            if (a.Right <= b.X)
                hGap = b.X - a.Right;
            else if (b.Right <= a.X)
                hGap = a.X - b.Right;

            return (Math.Round(vGap, 2), Math.Round(hGap, 2));
        }
    }
}
