// =======================================================
// File: AutoSlopeGeometry.cs
// Namespace: Revit26_Plugin_Revised.AutoSlopeByPoint.V032.Infrastructure.RoofOps
// Moved from V030 Core/Engine (it uses the Revit API, so it belongs in
// Infrastructure). Changes:
//   - GetClosestLoopIndex measures in plan (XY): the point is projected at
//     each curve's own height, so a pick taken on the old (sloped) surface
//     is not pushed towards whichever loop happens to sit higher (D3).
//   - IsPointOnFace removed (unused in V030).
// =======================================================

using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin_Revised.AutoSlopeByPoint.V032.Infrastructure.RoofOps
{
    internal static class AutoSlopeGeometry
    {
        /// <summary>Every Arc on the top face's loops (outer boundary and openings).</summary>
        public static List<Arc> GetBoundaryArcs(Face topFace)
        {
            var arcs = new List<Arc>();
            if (topFace == null) return arcs;

            foreach (EdgeArray loop in topFace.EdgeLoops)
                foreach (Edge edge in loop)
                    if (edge.AsCurve() is Arc arc)
                        arcs.Add(arc);
            return arcs;
        }

        /// <summary>Every straight Line edge on the top face's loops — the roof's real edges.</summary>
        public static List<Line> GetBoundaryLines(Face topFace)
        {
            var lines = new List<Line>();
            if (topFace == null) return lines;

            foreach (EdgeArray loop in topFace.EdgeLoops)
                foreach (Edge edge in loop)
                    if (edge.AsCurve() is Line line)
                        lines.Add(line);
            return lines;
        }

        /// <summary>Each top-face loop (outer boundary and each opening) as its own curve list.</summary>
        public static List<List<Curve>> GetBoundaryLoops(Face topFace)
        {
            var loops = new List<List<Curve>>();
            if (topFace == null) return loops;

            foreach (EdgeArray loop in topFace.EdgeLoops)
            {
                var curves = new List<Curve>();
                foreach (Edge edge in loop)
                {
                    Curve c = edge.AsCurve();
                    if (c != null) curves.Add(c);
                }
                if (curves.Count > 0) loops.Add(curves);
            }
            return loops;
        }

        /// <summary>Index of the loop closest to the point in plan, or -1.</summary>
        public static int GetClosestLoopIndex(XYZ point, List<List<Curve>> loops)
        {
            if (point == null || loops == null || loops.Count == 0) return -1;

            int bestIndex = -1;
            double bestDist = double.MaxValue;

            for (int i = 0; i < loops.Count; i++)
            {
                foreach (Curve curve in loops[i])
                {
                    // Bring the point to the curve's height so only plan distance counts.
                    XYZ flat = new XYZ(point.X, point.Y, curve.GetEndPoint(0).Z);
                    IntersectionResult proj = curve.Project(flat);
                    if (proj == null) continue;

                    XYZ q = proj.XYZPoint;
                    double dx = q.X - point.X, dy = q.Y - point.Y;
                    double dist = dx * dx + dy * dy;
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestIndex = i;
                    }
                }
            }
            return bestIndex;
        }

        /// <summary>The roof face whose centre sits highest (unchanged from V030).</summary>
        public static Face GetTopFace(RoofBase roof)
        {
            if (roof == null) return null;

            var opt = new Options { ComputeReferences = false, IncludeNonVisibleObjects = false };
            GeometryElement geom = roof.get_Geometry(opt);
            if (geom == null) return null;

            Face topFace = null;
            double maxZ = double.MinValue;

            foreach (GeometryObject obj in geom)
            {
                if (obj is not Solid solid || solid.Faces.Size == 0) continue;

                foreach (Face face in solid.Faces)
                {
                    BoundingBoxUV bb = face.GetBoundingBox();
                    if (bb == null) continue;

                    var mid = new UV((bb.Min.U + bb.Max.U) * 0.5, (bb.Min.V + bb.Max.V) * 0.5);
                    XYZ p = face.Evaluate(mid);
                    if (p == null) continue;

                    if (p.Z > maxZ)
                    {
                        maxZ = p.Z;
                        topFace = face;
                    }
                }
            }
            return topFace;
        }
    }
}
