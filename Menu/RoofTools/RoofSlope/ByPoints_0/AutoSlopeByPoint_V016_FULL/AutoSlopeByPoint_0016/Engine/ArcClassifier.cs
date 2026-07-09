// =======================================================
// File: ArcClassifier.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.V016
// Purpose: Classify each boundary/opening arc as either:
//   - Convex obstacle: the arc bulges INTO the walkable roof
//     surface (a protrusion or opening edge). The straight chord
//     between its two endpoints falls OFF the valid top face, so
//     any path crossing here must hug the arc via tangent lines.
//   - Concave notch: the arc bulges AWAY from the walkable surface
//     (a bite taken out of the boundary). The straight chord between
//     its endpoints stays ON the valid top face, so it is NOT an
//     obstacle — a plain straight edge across the chord is valid and
//     tangent routing is unnecessary (this matches the existing
//     same-arc chord-bypass fix already in DijkstraPathEngine).
//
// Test: sample the chord midpoint (endpoint average) and the arc's
// own midpoint. If the CHORD midpoint still projects inside the top
// face, nothing about this arc obstructs a straight line -> Concave.
// If the chord midpoint falls outside the top face, the arc really is
// in the way -> Convex obstacle.
// =======================================================

using Autodesk.Revit.DB;

namespace Revit26_Plugin.AutoSlopeByPoint.V016.Core.Engine
{
    public enum ArcConcavity
    {
        Convex,   // obstacle — requires tangent routing
        Concave   // notch — straight chord is fine
    }

    public static class ArcClassifier
    {
        private const double PROJ_TOL = 0.00328084; // ~1mm, matches DijkstraPathEngine.PROJ_TOL

        public static ArcConcavity Classify(Arc arc, Face topFace)
        {
            XYZ p0 = arc.GetEndPoint(0);
            XYZ p1 = arc.GetEndPoint(1);
            XYZ chordMid = (p0 + p1) * 0.5;

            return PointOnFace(chordMid, topFace)
                ? ArcConcavity.Concave   // chord already valid — not an obstacle
                : ArcConcavity.Convex;   // chord falls outside the face — real obstacle
        }

        private static bool PointOnFace(XYZ p, Face face)
        {
            IntersectionResult proj = face.Project(p);
            if (proj == null)
            {
                proj = face.Project(p + XYZ.BasisZ * PROJ_TOL)
                    ?? face.Project(p - XYZ.BasisZ * PROJ_TOL);
                if (proj == null) return false;
            }

            try
            {
                return face.IsInside(proj.UVPoint);
            }
            catch
            {
                BoundingBoxUV bb = face.GetBoundingBox();
                UV uv = proj.UVPoint;
                return uv.U >= bb.Min.U && uv.U <= bb.Max.U &&
                       uv.V >= bb.Min.V && uv.V <= bb.Max.V;
            }
        }
    }
}
