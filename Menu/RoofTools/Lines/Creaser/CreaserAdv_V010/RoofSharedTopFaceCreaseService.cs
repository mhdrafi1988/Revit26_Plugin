// ============================================================
// File: RoofSharedTopFaceCreaseService.cs
// Namespace: Revit26_Plugin.CreaserAdv.V010.Services
// ============================================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv.V010.Services
{
    /// <summary>Crease and boundary curves pulled from one roof in a single geometry pass.</summary>
    public sealed class RoofCurveSet
    {
        public IList<Curve> Creases  { get; } = new List<Curve>();
        public IList<Curve> Boundary { get; } = new List<Curve>();
    }

    /// <summary>
    /// Extracts roof crease curves and, optionally, boundary curves
    /// by inspecting solid-topology edges.
    ///
    /// Crease edge  = neither adjacent face is a side face AND
    ///                at least one adjacent face is a top face.
    ///
    /// Boundary edge = exactly one adjacent face is a top face AND
    ///                 the other is a side face.
    ///
    /// Works for planar, cylindrical, conical, and freeform roofs by
    /// sampling face normals over a UV grid.
    ///
    /// V010: one geometry pass classifies each edge once for both sets (V009
    /// walked and re-classified every edge twice when boundary lines were
    /// on); solids nested inside <see cref="GeometryInstance"/>s are now
    /// included; and every stage reports counts to the UI log so an empty
    /// result can be traced to its cause.
    /// </summary>
    public sealed class RoofSharedTopFaceCreaseService
    {
        private readonly LoggingService _log;

        private const double UpwardMajorityThreshold   = 0.7;
        private const double VerticalMajorityThreshold = 0.7;
        private const double TopFaceMinNormalZ          = 0.3;
        private const double SideFaceMaxNormalZ         = 0.2;
        private const int    SampleCount                = 5;

        public RoofSharedTopFaceCreaseService(LoggingService log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        // --------------------------------------------------
        // Public API
        // --------------------------------------------------

        /// <summary>
        /// Classifies every solid edge of <paramref name="roof"/> and returns
        /// crease curves (top/top) and, when requested, boundary curves (top/side).
        /// </summary>
        public RoofCurveSet Extract(Element roof, bool includeBoundary)
        {
            var set = new RoofCurveSet();

            var solids = new List<Solid>();
            CollectSolids(roof?.get_Geometry(new Options { ComputeReferences = true, IncludeNonVisibleObjects = false }), solids);

            if (roof == null || solids.Count == 0)
            {
                _log.Warning("Roof has no solid geometry in this view/detail level — nothing to extract. " +
                             "Check that the roof is visible in the current view and is not empty.");
                return set;
            }

            int edgesTotal = 0, degenerate = 0, topTop = 0, topSide = 0, sideSide = 0, underside = 0, unclassified = 0;
            int faceCount = 0;

            foreach (Solid solid in solids)
            {
                faceCount += solid.Faces.Size;

                foreach (Edge edge in solid.Edges)
                {
                    edgesTotal++;

                    Curve curve = edge.AsCurve();
                    if (curve == null || curve.Length < 1e-6) { degenerate++; continue; }

                    Face f0 = edge.GetFace(0);
                    Face f1 = edge.GetFace(1);

                    bool f0Side = IsSideFace(f0), f1Side = IsSideFace(f1);
                    bool f0Top  = IsTopFace(f0),  f1Top  = IsTopFace(f1);

                    if (f0Side && f1Side)
                    {
                        sideSide++;
                    }
                    else if (f0Side || f1Side)
                    {
                        if (f0Top || f1Top)
                        {
                            topSide++;
                            if (includeBoundary)
                                set.Boundary.Add(NormalizeOrientation(curve));
                        }
                        else
                        {
                            underside++; // side/bottom edge — not part of the top surface
                        }
                    }
                    else if (f0Top || f1Top)
                    {
                        topTop++;
                        set.Creases.Add(NormalizeOrientation(curve));
                    }
                    else
                    {
                        unclassified++; // neither top nor side (e.g. underside/underside)
                    }
                }
            }

            _log.Info($"Roof geometry: {solids.Count} solid(s), {faceCount} face(s), {edgesTotal} edge(s).");
            _log.Debug($"  Edge classification — top/top (creases): {topTop}, top/side (boundary): {topSide}, " +
                       $"side/side: {sideSide}, side/underside: {underside}, underside/other: {unclassified}, degenerate: {degenerate}.");
            _log.Info($"Crease edges extracted: {set.Creases.Count}");

            if (includeBoundary)
                _log.Info($"Boundary edges extracted: {set.Boundary.Count}");

            return set;
        }

        // --------------------------------------------------
        // Geometry traversal
        // --------------------------------------------------

        private static void CollectSolids(GeometryElement geom, List<Solid> solids)
        {
            if (geom == null) return;

            foreach (GeometryObject obj in geom)
            {
                switch (obj)
                {
                    case Solid solid when !solid.Edges.IsEmpty:
                        solids.Add(solid);
                        break;

                    case GeometryInstance instance:
                        // Instance geometry is already transformed into model coordinates.
                        CollectSolids(instance.GetInstanceGeometry(), solids);
                        break;
                }
            }
        }

        // --------------------------------------------------
        // Face classification
        // --------------------------------------------------

        private static bool IsTopFace(Face face)
        {
            if (face == null) return false;
            return MajorityCheck(face, n => n.Z > TopFaceMinNormalZ, UpwardMajorityThreshold);
        }

        private static bool IsSideFace(Face face)
        {
            if (face == null) return false;
            return MajorityCheck(face, n => Math.Abs(n.Z) < SideFaceMaxNormalZ, VerticalMajorityThreshold);
        }

        private static bool MajorityCheck(Face face, Func<XYZ, bool> predicate, double threshold)
        {
            BoundingBoxUV bb = face.GetBoundingBox();
            int pass = 0, total = 0;

            for (int i = 0; i < SampleCount; i++)
            {
                for (int j = 0; j < SampleCount; j++)
                {
                    double u = bb.Min.U + (bb.Max.U - bb.Min.U) * i / (SampleCount - 1);
                    double v = bb.Min.V + (bb.Max.V - bb.Min.V) * j / (SampleCount - 1);
                    try
                    {
                        if (predicate(face.ComputeNormal(new UV(u, v)))) pass++;
                        total++;
                    }
                    catch (System.Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"[RoofSharedTopFaceCreaseService] MajorityCheck: ComputeNormal failed at U={u:F3} V={v:F3} — {ex.Message}");
                    }
                }
            }

            return total > 0 && (double)pass / total >= threshold;
        }

        // --------------------------------------------------
        // Orientation normalisation (high-Z start)
        // --------------------------------------------------

        private static Curve NormalizeOrientation(Curve curve)
        {
            XYZ start = curve.GetEndPoint(0);
            XYZ end   = curve.GetEndPoint(1);
            XYZ mid   = curve.Evaluate(0.5, true);

            double avgStartZ = (start.Z + mid.Z) / 2.0;
            double avgEndZ   = (end.Z   + mid.Z) / 2.0;

            return avgStartZ >= avgEndZ ? curve : curve.CreateReversed();
        }
    }
}
