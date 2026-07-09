// =======================================================
// File: CurveIntersectionHelper.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.V011
// Purpose:
//   - Detect which SlabShapeVertex points already sit on a boundary/opening Arc.
//   - Detect where a straight line between two vertices enters/exits an Arc
//     (partial overlap case) and insert real SlabShapeVertex points there.
//   - Provide arc-length lookup for two points known to lie on the same Arc.
// =======================================================

using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPoint.V011.Core.Engine
{
    /// <summary>Maps a vertex position to the Arc it lies on (if any) and its parameter on that Arc.</summary>
    public class VertexCurveInfo
    {
        public Arc Curve;
        public double Parameter;
    }

    public static class CurveIntersectionHelper
    {
        /// <summary>
        /// For each position, checks whether it lies on any of the given arcs
        /// (within tolerance). Returns a map keyed by vertex index.
        /// </summary>
        public static Dictionary<int, VertexCurveInfo> MapVerticesOnCurves(
            List<XYZ> positions, List<Arc> arcs, double toleranceFt)
        {
            var map = new Dictionary<int, VertexCurveInfo>();

            for (int i = 0; i < positions.Count; i++)
            {
                XYZ p = positions[i];
                foreach (Arc arc in arcs)
                {
                    IntersectionResult proj = arc.Project(p);
                    if (proj == null) continue;

                    if (proj.XYZPoint.DistanceTo(p) <= toleranceFt)
                    {
                        map[i] = new VertexCurveInfo { Curve = arc, Parameter = proj.Parameter };
                        break; // first matching arc wins
                    }
                }
            }
            return map;
        }

        /// <summary>
        /// Exact distance between two parameters on the SAME arc (not the chord).
        /// </summary>
        public static double ArcLengthBetween(Arc arc, double paramA, double paramB)
        {
            double lo = paramA < paramB ? paramA : paramB;
            double hi = paramA < paramB ? paramB : paramA;

            Curve sub = arc.Clone();
            sub.MakeBound(lo, hi);
            return sub.Length;
        }

        /// <summary>
        /// Checks every candidate vertex pair (within edgeThresholdFt) for a straight-line
        /// intersection with any boundary arc, and inserts new real SlabShapeVertex points
        /// at the entry/exit locations found. Must be called inside an active Transaction.
        /// Returns the number of points inserted.
        /// </summary>
        public static int InsertIntersectionPoints(
            SlabShapeEditor editor,
            List<XYZ> vertexPositions,
            List<Arc> arcs,
            double edgeThresholdFt,
            double toleranceFt,
            System.Action<string> log = null)
        {
            if (arcs == null || arcs.Count == 0) return 0;

            var toInsert = new List<XYZ>();
            int n = vertexPositions.Count;

            for (int i = 0; i < n; i++)
            {
                XYZ a = vertexPositions[i];
                for (int j = i + 1; j < n; j++)
                {
                    XYZ b = vertexPositions[j];
                    double dist = a.DistanceTo(b);
                    if (dist < 0.033 || dist > edgeThresholdFt) continue;

                    Line line;
                    try { line = Line.CreateBound(a, b); }
                    catch { continue; } // zero-length / invalid

                    foreach (Arc arc in arcs)
                    {
                        SetComparisonResult result;
                        IntersectionResultArray xsects;
                        try
                        {
                            result = line.Intersect(arc, out xsects);
                        }
                        catch
                        {
                            continue;
                        }

                        if (result != SetComparisonResult.Overlap || xsects == null) continue;

                        foreach (IntersectionResult ir in xsects)
                        {
                            XYZ pt = ir.XYZPoint;
                            if (pt == null) continue;

                            // Skip if it basically coincides with an existing vertex/endpoint.
                            if (pt.DistanceTo(a) <= toleranceFt || pt.DistanceTo(b) <= toleranceFt)
                                continue;

                            bool alreadyQueued = false;
                            foreach (XYZ existing in toInsert)
                            {
                                if (existing.DistanceTo(pt) <= toleranceFt) { alreadyQueued = true; break; }
                            }
                            if (alreadyQueued) continue;

                            bool alreadyVertex = false;
                            foreach (XYZ v in vertexPositions)
                            {
                                if (v.DistanceTo(pt) <= toleranceFt) { alreadyVertex = true; break; }
                            }
                            if (alreadyVertex) continue;

                            toInsert.Add(pt);
                        }
                    }
                }
            }

            foreach (XYZ pt in toInsert)
            {
                editor.AddPoint(pt);
            }

            log?.Invoke($"Curve intersection check: inserted {toInsert.Count} new point(s) on arc boundaries/openings.");
            return toInsert.Count;
        }
    }
}
