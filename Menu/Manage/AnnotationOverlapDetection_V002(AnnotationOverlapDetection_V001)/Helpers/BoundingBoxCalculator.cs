using Autodesk.Revit.DB;
using Revit26_Plugin.AnnotationOverlapDetection.V002.Models;
using System;

namespace Revit26_Plugin.AnnotationOverlapDetection.V002.Helpers
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
        /// Reads an element's bounding box (as seen in the given view) and
        /// converts it to (x, y, width, height) in mm. Returns null if the
        /// element has no bounding box in this view (e.g. hidden).
        /// </summary>
        public static (double x, double y, double width, double height)? GetBoundingBox(Element elem, View view)
        {
            BoundingBoxXYZ bbox = elem.get_BoundingBox(view);
            if (bbox == null)
                return null;

            double x = bbox.Min.X * FeetToMm;
            double y = bbox.Min.Y * FeetToMm;
            double width = (bbox.Max.X - bbox.Min.X) * FeetToMm;
            double height = (bbox.Max.Y - bbox.Min.Y) * FeetToMm;

            return (x, y, width, height);
        }

        /// <summary>
        /// Insertion point in mm - defaults to the bounding box min corner.
        /// (Elements with a LocationPoint could be handled more precisely by
        /// the caller before falling back to this.)
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
