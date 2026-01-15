// ==================================================
// File: RoofSharedTopFaceCreaseService.cs
// ==================================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    /// <summary>
    /// Extracts TRUE roof creases using solid topology.
    ///
    /// Rule (Option 1):
    /// - Edge is kept if:
    ///   - At least ONE adjacent face is a top roof face
    ///   - NO adjacent face is a vertical side face
    ///
    /// This correctly handles:
    /// - Straight roofs
    /// - Rectangular openings
    /// - Round openings (cylindrical faces)
    /// - Stepped roofs
    /// </summary>
    public sealed class RoofSharedTopFaceCreaseService
    {
        private readonly LoggingService _log;

        public RoofSharedTopFaceCreaseService(LoggingService log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// Returns normalized 3D crease lines (p1 = higher Z).
        /// </summary>
        public IList<Line> ExtractSharedTopFaceCreases(Element roof)
        {
            var result = new List<Line>();

            if (roof == null)
                return result;

            var options = new Options
            {
                ComputeReferences = false,
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
                    // Only straight edges produce linear creases
                    if (edge.AsCurve() is not Line raw)
                        continue;

                    Face f0 = edge.GetFace(0);
                    Face f1 = edge.GetFace(1);

                    // ❌ Remove edges touching vertical side faces
                    if (IsSideFace(f0) || IsSideFace(f1))
                        continue;

                    // ✅ Keep edges with at least one top face
                    if (!IsTopFace(f0) && !IsTopFace(f1))
                        continue;

                    XYZ a = raw.GetEndPoint(0);
                    XYZ b = raw.GetEndPoint(1);

                    if (a.DistanceTo(b) < 1e-6)
                        continue;

                    // Normalize direction (higher Z first)
                    XYZ p1 = a.Z >= b.Z ? a : b;
                    XYZ p2 = a.Z >= b.Z ? b : a;

                    result.Add(Line.CreateBound(p1, p2));
                }
            }

            _log.Info($"Top-face creases extracted (Option 1): {result.Count}");
            return result;
        }

        // ==================================================
        // Face classification helpers
        // ==================================================

        /// <summary>
        /// True if face is a roof top face (planar, upward-facing).
        /// </summary>
        private static bool IsTopFace(Face face)
        {
            if (face is not PlanarFace pf)
                return false;

            // Accept moderately sloped roofs
            return pf.FaceNormal.Z >= 0.4;
        }

        /// <summary>
        /// True if face is a vertical / near-vertical side face.
        /// </summary>
        private static bool IsSideFace(Face face)
        {
            if (face is not PlanarFace pf)
                return false;

            // Near-vertical faces (walls, edges, openings)
            return Math.Abs(pf.FaceNormal.Z) < 0.2;
        }
    }
}
