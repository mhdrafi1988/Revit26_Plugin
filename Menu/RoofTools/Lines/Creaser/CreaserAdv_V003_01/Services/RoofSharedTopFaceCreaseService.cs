// ============================================================
// File: RoofSharedTopFaceCreaseService.cs
// Namespace: Revit26_Plugin.CreaserAdv_V003_01
// ============================================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V003_01.Services
{
    /// <summary>
    /// Extracts roof crease curves by inspecting solid-topology edges.
    /// An edge qualifies as a crease when:
    ///   • neither adjacent face is a side (near-vertical) face, AND
    ///   • at least one adjacent face is a top (upward-facing) face.
    ///
    /// This reliably excludes:
    ///   – outer perimeter edges  (top face + side face)
    ///   – opening boundary edges (top face + side face)
    ///   – bottom face edges
    ///
    /// Works for planar, cylindrical, conical, and freeform roof surfaces
    /// by sampling face normals over a UV grid instead of reading a single
    /// planar normal.
    /// </summary>
    public sealed class RoofSharedTopFaceCreaseService
    {
        private readonly LoggingService _log;

        // Fraction of sampled UV points that must be upward for "top face".
        private const double UpwardMajorityThreshold  = 0.7;

        // Fraction of sampled UV points that must be vertical for "side face".
        private const double VerticalMajorityThreshold = 0.7;

        // Z-component thresholds
        private const double TopFaceMinNormalZ  =  0.3;  // normal points ≥ 30° from horizontal
        private const double SideFaceMaxNormalZ =  0.2;  // normal within 20° of horizontal

        // UV grid resolution per face
        private const int SampleCount = 5;

        public RoofSharedTopFaceCreaseService(LoggingService log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        // --------------------------------------------------
        // Public API
        // --------------------------------------------------

        public IList<Curve> ExtractSharedTopFaceCreases(Element roof)
        {
            var result = new List<Curve>();

            if (roof == null)
                return result;

            var options = new Options
            {
                ComputeReferences      = true,
                IncludeNonVisibleObjects = false
            };

            GeometryElement geom = roof.get_Geometry(options);

            if (geom == null)
            {
                _log.Warning("Roof geometry not found.");
                return result;
            }

            foreach (GeometryObject obj in geom)
            {
                if (obj is not Solid solid || solid.Edges.IsEmpty)
                    continue;

                foreach (Edge edge in solid.Edges)
                {
                    Curve curve = edge.AsCurve();

                    if (curve == null || curve.Length < 1e-6)
                        continue;

                    Face f0 = edge.GetFace(0);
                    Face f1 = edge.GetFace(1);

                    // Exclude any edge touching a side face.
                    if (IsSideFace(f0) || IsSideFace(f1))
                        continue;

                    // At least one face must be a top face.
                    if (!IsTopFace(f0) && !IsTopFace(f1))
                        continue;

                    result.Add(NormalizeOrientation(curve));
                }
            }

            _log.Info($"Top-face creases extracted: {result.Count}");
            return result;
        }

        // --------------------------------------------------
        // Face classification
        // --------------------------------------------------

        /// <summary>
        /// A face is considered a "top" face when the majority of its sampled
        /// UV normals point upward (Z ≥ <see cref="TopFaceMinNormalZ"/>).
        /// </summary>
        private static bool IsTopFace(Face face)
        {
            if (face == null) return false;

            BoundingBoxUV bb = face.GetBoundingBox();
            int upward = 0, total = 0;

            for (int i = 0; i < SampleCount; i++)
            {
                for (int j = 0; j < SampleCount; j++)
                {
                    UV uv = SampleUV(bb, i, j);
                    try
                    {
                        if (face.ComputeNormal(uv).Z > TopFaceMinNormalZ)
                            upward++;
                        total++;
                    }
                    catch { /* skip degenerate UV */ }
                }
            }

            return total > 0 && (double)upward / total >= UpwardMajorityThreshold;
        }

        /// <summary>
        /// A face is a "side" face when the majority of its sampled normals
        /// are near-horizontal (|Z| &lt; <see cref="SideFaceMaxNormalZ"/>).
        /// </summary>
        private static bool IsSideFace(Face face)
        {
            if (face == null) return false;

            BoundingBoxUV bb = face.GetBoundingBox();
            int vertical = 0, total = 0;

            for (int i = 0; i < SampleCount; i++)
            {
                for (int j = 0; j < SampleCount; j++)
                {
                    UV uv = SampleUV(bb, i, j);
                    try
                    {
                        if (Math.Abs(face.ComputeNormal(uv).Z) < SideFaceMaxNormalZ)
                            vertical++;
                        total++;
                    }
                    catch { /* skip degenerate UV */ }
                }
            }

            return total > 0 && (double)vertical / total >= VerticalMajorityThreshold;
        }

        // --------------------------------------------------
        // Helpers
        // --------------------------------------------------

        private static UV SampleUV(BoundingBoxUV bb, int i, int j)
        {
            double u = bb.Min.U + (bb.Max.U - bb.Min.U) * i / (SampleCount - 1);
            double v = bb.Min.V + (bb.Max.V - bb.Min.V) * j / (SampleCount - 1);
            return new UV(u, v);
        }

        /// <summary>
        /// Reverses the curve if its start endpoint is at a lower Z than its end,
        /// ensuring consistent "high-Z first" orientation across all creases.
        /// </summary>
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
