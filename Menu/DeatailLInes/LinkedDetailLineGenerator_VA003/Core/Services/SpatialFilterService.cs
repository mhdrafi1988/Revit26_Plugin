using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Services
{
    /// <summary>
    /// Stage 1 filtering (spec Section 13): cheap bounding-box test against the
    /// processing boundary, applied AFTER Category/Family/Type filtering and BEFORE
    /// any geometry extraction. Purpose: avoid extracting/transforming geometry for
    /// elements nowhere near the active view (performance requirement, Section 32).
    /// </summary>
    public class SpatialFilterService
    {
        /// <summary>
        /// Returns only elements whose bounding box (transformed into host coordinates)
        /// intersects or is inside the processing boundary's bounding box. This is a
        /// coarse pre-filter — Stage 2 (GeometryClippingService) does the accurate
        /// per-curve boundary test after extraction.
        /// </summary>
        public List<Element> FilterCandidates(
            IEnumerable<Element> linkedElements,
            Transform linkToHostTransform,
            List<XYZ> processingBoundary,
            Document linkedDoc)
        {
            // Boundary's own bounding box for a fast reject test.
            double minX = processingBoundary.Min(p => p.X);
            double maxX = processingBoundary.Max(p => p.X);
            double minY = processingBoundary.Min(p => p.Y);
            double maxY = processingBoundary.Max(p => p.Y);

            var result = new List<Element>();

            foreach (var elem in linkedElements)
            {
                BoundingBoxXYZ? bbox = elem.get_BoundingBox(null);
                if (bbox == null) continue;

                // Transform the 8 corners of the element's bbox into host coordinates
                // and take their bounding box — cheap and conservative (never excludes
                // a true candidate, may include a few extra that Stage 2 will reject).
                var corners = GetBoxCorners(bbox);
                double hMinX = double.MaxValue, hMaxX = double.MinValue;
                double hMinY = double.MaxValue, hMaxY = double.MinValue;

                foreach (var c in corners)
                {
                    XYZ hostPt = linkToHostTransform.OfPoint(c);
                    hMinX = System.Math.Min(hMinX, hostPt.X);
                    hMaxX = System.Math.Max(hMaxX, hostPt.X);
                    hMinY = System.Math.Min(hMinY, hostPt.Y);
                    hMaxY = System.Math.Max(hMaxY, hostPt.Y);
                }

                bool intersects = hMinX <= maxX && hMaxX >= minX && hMinY <= maxY && hMaxY >= minY;
                if (intersects)
                    result.Add(elem);
            }

            return result;
        }

        private static IEnumerable<XYZ> GetBoxCorners(BoundingBoxXYZ bbox)
        {
            var min = bbox.Min;
            var max = bbox.Max;
            var localCorners = new[]
            {
                new XYZ(min.X, min.Y, min.Z), new XYZ(max.X, min.Y, min.Z),
                new XYZ(max.X, max.Y, min.Z), new XYZ(min.X, max.Y, min.Z),
                new XYZ(min.X, min.Y, max.Z), new XYZ(max.X, min.Y, max.Z),
                new XYZ(max.X, max.Y, max.Z), new XYZ(min.X, max.Y, max.Z),
            };
            return localCorners.Select(p => bbox.Transform.OfPoint(p));
        }
    }
}
