using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRoomsViaPlanView.V004
{
    public class BoundaryResult
    {
        public List<CurveLoop> Loops { get; set; } = new(); // [0] = outer, rest = inner
        public bool OuterValid { get; set; }
        public bool WasTrimmedOrFixed { get; set; }
        public int InnerLoopsSkipped { get; set; }
        public string FailureReason { get; set; }
    }

    public static class RoomBoundaryService
    {
        private const double DedupeToleranceFt = 1.0 / 16.0 / 12.0; // 1/16" in feet

        public static BoundaryResult BuildLoops(Room room, Transform transform)
        {
            var result = new BoundaryResult();

            var options = new SpatialElementBoundaryOptions
            {
                SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish
            };

            IList<IList<BoundarySegment>> segmentLoops;
            try
            {
                segmentLoops = room.GetBoundarySegments(options);
            }
            catch (Exception ex)
            {
                result.FailureReason = $"Could not read boundary segments: {ex.Message}";
                return result;
            }

            if (segmentLoops == null || segmentLoops.Count == 0)
            {
                result.FailureReason = "Room has no boundary segments (unplaced or unbounded).";
                return result;
            }

            for (int loopIndex = 0; loopIndex < segmentLoops.Count; loopIndex++)
            {
                bool isOuter = loopIndex == 0;
                var (loop, trimmed) = BuildSingleLoop(segmentLoops[loopIndex], transform);

                if (loop == null)
                {
                    if (isOuter)
                    {
                        result.FailureReason = "Outer boundary loop failed validation (self-intersecting or non-planar).";
                        return result;
                    }
                    result.InnerLoopsSkipped++;
                    continue;
                }

                if (trimmed) result.WasTrimmedOrFixed = true;
                result.Loops.Add(loop);
            }

            result.OuterValid = result.Loops.Count > 0;
            return result;
        }

        private static (CurveLoop loop, bool trimmed) BuildSingleLoop(IList<BoundarySegment> segments, Transform transform)
        {
            bool trimmed = false;

            // Transform each segment's curve into host coordinates.
            var curves = segments.Select(s => s.GetCurve().CreateTransformed(transform)).ToList();

            // Dedupe near-duplicate consecutive points (tolerance ~1/16").
            var points = new List<XYZ> { curves[0].GetEndPoint(0) };
            foreach (var c in curves)
            {
                var end = c.GetEndPoint(1);
                if (end.DistanceTo(points[^1]) > DedupeToleranceFt)
                    points.Add(end);
                else
                    trimmed = true; // merged a near-duplicate point
            }

            // Ensure closed.
            if (points.Count > 1 && points[0].DistanceTo(points[^1]) > 1e-6)
            {
                if (points[0].DistanceTo(points[^1]) <= DedupeToleranceFt)
                {
                    points[^1] = points[0]; // snap-close
                    trimmed = true;
                }
                else
                {
                    points.Add(points[0]); // explicit closing segment
                    trimmed = true;
                }
            }

            if (points.Count < 4) // need at least 3 unique + closing point
                return (null, trimmed);

            try
            {
                var loop = new CurveLoop();
                for (int i = 0; i < points.Count - 1; i++)
                {
                    var line = Line.CreateBound(points[i], points[i + 1]);
                    loop.Append(line);
                }

                if (!IsPlanar(loop)) return (null, trimmed);
                if (HasSelfIntersection(points)) return (null, trimmed);

                return (loop, trimmed);
            }
            catch
            {
                return (null, trimmed);
            }
        }

        private static bool IsPlanar(CurveLoop loop)
        {
            try
            {
                loop.GetPlane();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Simple O(n^2) segment-intersection check on the 2D (X,Y) projection.
        /// Fine for typical room boundaries (rarely more than a few dozen points).
        /// </summary>
        private static bool HasSelfIntersection(List<XYZ> pts)
        {
            int n = pts.Count - 1; // last point duplicates first
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    if (Math.Abs(i - j) <= 1) continue;
                    if (i == 0 && j == n - 1) continue; // adjacent through the closing edge

                    if (SegmentsIntersect2D(pts[i], pts[i + 1], pts[j], pts[j + 1]))
                        return true;
                }
            }
            return false;
        }

        private static bool SegmentsIntersect2D(XYZ p1, XYZ p2, XYZ p3, XYZ p4)
        {
            double d1 = Cross(p4 - p3, p1 - p3);
            double d2 = Cross(p4 - p3, p2 - p3);
            double d3 = Cross(p2 - p1, p3 - p1);
            double d4 = Cross(p2 - p1, p4 - p1);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0))
                && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        private static double Cross(XYZ a, XYZ b) => a.X * b.Y - a.Y * b.X;
    }
}
