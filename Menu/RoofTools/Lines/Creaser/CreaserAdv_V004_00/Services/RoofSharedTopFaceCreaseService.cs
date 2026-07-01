// ============================================================
// File: RoofSharedTopFaceCreaseService.cs
// Namespace: Revit26_Plugin.CreaserAdv_V004_00
// ============================================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V004_00.Services
{
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
        // Crease extraction  (top-face / top-face edges)
        // --------------------------------------------------

        public IList<Curve> ExtractSharedTopFaceCreases(Element roof)
        {
            var result = new List<Curve>();

            foreach (Edge edge in GetSolidEdges(roof))
            {
                Curve curve = edge.AsCurve();
                if (curve == null || curve.Length < 1e-6) continue;

                Face f0 = edge.GetFace(0);
                Face f1 = edge.GetFace(1);

                if (IsSideFace(f0) || IsSideFace(f1))    continue;
                if (!IsTopFace(f0) && !IsTopFace(f1))    continue;

                result.Add(NormalizeOrientation(curve));
            }

            _log.Info($"Crease edges extracted: {result.Count}");
            return result;
        }

        // --------------------------------------------------
        // Boundary extraction  (top-face / side-face edges)
        // --------------------------------------------------

        public IList<Curve> ExtractBoundaryLines(Element roof)
        {
            var result = new List<Curve>();

            foreach (Edge edge in GetSolidEdges(roof))
            {
                Curve curve = edge.AsCurve();
                if (curve == null || curve.Length < 1e-6) continue;

                Face f0 = edge.GetFace(0);
                Face f1 = edge.GetFace(1);

                bool f0Top  = IsTopFace(f0),  f1Top  = IsTopFace(f1);
                bool f0Side = IsSideFace(f0), f1Side = IsSideFace(f1);

                // Exactly one top + one side = perimeter/opening boundary
                bool isBoundary = (f0Top && f1Side) || (f1Top && f0Side);
                if (!isBoundary) continue;

                result.Add(NormalizeOrientation(curve));
            }

            _log.Info($"Boundary edges extracted: {result.Count}");
            return result;
        }

        // --------------------------------------------------
        // Shared geometry helper
        // --------------------------------------------------

        private IEnumerable<Edge> GetSolidEdges(Element roof)
        {
            if (roof == null) yield break;

            var options = new Options
            {
                ComputeReferences      = true,
                IncludeNonVisibleObjects = false
            };

            GeometryElement geom = roof.get_Geometry(options);
            if (geom == null)
            {
                _log.Warning("Roof geometry not found.");
                yield break;
            }

            foreach (GeometryObject obj in geom)
            {
                if (obj is Solid solid && !solid.Edges.IsEmpty)
                    foreach (Edge edge in solid.Edges)
                        yield return edge;
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
                    catch { }
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
