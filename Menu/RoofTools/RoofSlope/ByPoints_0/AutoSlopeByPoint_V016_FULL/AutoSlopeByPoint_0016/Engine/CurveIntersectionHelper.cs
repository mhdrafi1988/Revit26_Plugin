// =======================================================
// File: CurveIntersectionHelper.cs
// Full corrected version with Fix #2 (arc length wrapping)
// =======================================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPoint.V016.Core.Engine
{
    public class VertexCurveInfo
    {
        public Arc Curve;
        public double Parameter;
    }

    public static class CurveIntersectionHelper
    {
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
                        break;
                    }
                }
            }
            return map;
        }

        // FIX #2: Correct arc length with wrapping
        public static double ArcLengthBetween(Arc arc, double paramA, double paramB)
        {
            double totalLen = arc.Length;
            double radius = arc.Radius;

            // Raw angular difference
            double rawAngle = Math.Abs(paramA - paramB);
            // Shortest angle (wrap around)
            double shortestAngle = Math.Min(rawAngle, 2 * Math.PI - rawAngle);

            // Arc length = radius * angle
            double arcLen = radius * shortestAngle;

            // Safety clamp: never exceed the total arc length
            return Math.Min(arcLen, totalLen);
        }

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
                    catch { continue; }

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