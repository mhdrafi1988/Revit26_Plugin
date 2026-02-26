// ==================================
// File: RoofCreaseExtractionService.cs
// ==================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    /// <summary>
    /// Extracts ONLY internal roof creases:
    /// linear edges whose two adjacent faces are BOTH top-facing planar roof faces.
    ///
    /// This robustly removes:
    /// - Outer perimeter boundary edges (top face + side face)
    /// - Inner opening boundary edges (top face + side face)
    /// - Any exposed/non-crease edges
    /// </summary>
    public class RoofCreaseExtractionService
    {
        private readonly LoggingService _log;

        public RoofCreaseExtractionService(LoggingService log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// Extract internal crease lines in 3D (p1 is higher Z).
        /// </summary>
        public IList<Line> ExtractInternalCreaseLines(Element roof)
        {
            var result = new List<Line>();

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
                if (obj is not Solid solid || solid.Faces.IsEmpty || solid.Edges.IsEmpty)
                    continue;

                // ✅ Iterate real topology edges once (NOT via face loops)
                foreach (Edge edge in solid.Edges)
                {
                    // We only care about straight edges
                    if (edge.AsCurve() is not Line raw)
                        continue;

                    // In a proper solid, each edge has 2 adjacent faces.
                    // Perimeter edges are shared with a side face, not another top face.
                    Face f0 = edge.GetFace(0);
                    Face f1 = edge.GetFace(1);

                    if (!IsTopRoofFace(f0) || !IsTopRoofFace(f1))
                        continue; // ❌ removes outer + hole boundaries automatically

                    XYZ a = raw.GetEndPoint(0);
                    XYZ b = raw.GetEndPoint(1);

                    // Normalize: higher Z -> p1
                    XYZ p1 = a.Z >= b.Z ? a : b;
                    XYZ p2 = a.Z >= b.Z ? b : a;

                    if (p1.DistanceTo(p2) < 1e-6)
                        continue;

                    result.Add(Line.CreateBound(p1, p2));
                }
            }

            _log.Info($"Internal creases extracted (top-face/top-face): {result.Count}");
            return result;
        }

        /// <summary>
        /// Defines what counts as a "top roof face" for crease detection.
        /// Adjust the Z threshold if you want steeper slopes included/excluded.
        /// </summary>
        private static bool IsTopRoofFace(Face face)
        {
            if (face is not PlanarFace pf)
                return false;

            // Your current intent: accept moderately sloped roof planes too.
            return pf.FaceNormal.Z >= 0.4;
        }
    }
}
