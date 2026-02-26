// ==================================================
// File: RoofSharedTopFaceCreaseService.cs (Enhanced)
// ==================================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    /// <summary>
    /// Enhanced to handle curved surfaces (cylindrical, conical, freeform)
    /// </summary>
    public sealed class RoofSharedTopFaceCreaseService
    {
        private readonly LoggingService _log;
        private const double CURVE_TOLERANCE = 0.01; // For curve sampling

        public RoofSharedTopFaceCreaseService(LoggingService log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public IList<Curve> ExtractSharedTopFaceCreases(Element roof)
        {
            var result = new List<Curve>();

            if (roof == null)
                return result;

            var options = new Options
            {
                ComputeReferences = true, // Need references for curved faces
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
                    // ACCEPT ALL CURVE TYPES - no longer filter by Line only
                    Curve curve = edge.AsCurve();
                    if (curve == null || curve.Length < 1e-6)
                        continue;

                    Face f0 = edge.GetFace(0);
                    Face f1 = edge.GetFace(1);

                    // Enhanced face classification for curved surfaces
                    if (IsSideFaceEnhanced(f0) || IsSideFaceEnhanced(f1))
                        continue;

                    if (!IsTopFaceEnhanced(f0) && !IsTopFaceEnhanced(f1))
                        continue;

                    // For curved edges, we need to sample points to determine high/low
                    Curve normalizedCurve = NormalizeCurveOrientation(curve);
                    result.Add(normalizedCurve);
                }
            }

            _log.Info($"Top-face creases extracted (including curved): {result.Count}");
            return result;
        }

        /// <summary>
        /// Enhanced top face detection for curved surfaces
        /// </summary>
        private bool IsTopFaceEnhanced(Face face)
        {
            if (face == null) return false;

            // Sample multiple points to determine if face is generally upward-facing
            BoundingBoxUV bbox = face.GetBoundingBox();
            int samples = 5; // Sample 5x5 grid
            int upwardSamples = 0;
            int totalSamples = 0;

            for (int i = 0; i < samples; i++)
            {
                for (int j = 0; j < samples; j++)
                {
                    double u = bbox.Min.U + (bbox.Max.U - bbox.Min.U) * i / (samples - 1);
                    double v = bbox.Min.V + (bbox.Max.V - bbox.Min.V) * j / (samples - 1);

                    try
                    {
                        XYZ point = face.Evaluate(new UV(u, v));
                        XYZ normal = face.ComputeNormal(new UV(u, v));

                        // Check if normal points generally upward
                        if (normal.Z > 0.3) // More tolerant threshold
                            upwardSamples++;

                        totalSamples++;
                    }
                    catch
                    {
                        // Skip invalid UV coordinates
                    }
                }
            }

            // Face is considered "top" if majority of sampled normals point upward
            return totalSamples > 0 && (double)upwardSamples / totalSamples > 0.7;
        }

        /// <summary>
        /// Enhanced side face detection (vertical or near-vertical)
        /// </summary>
        private bool IsSideFaceEnhanced(Face face)
        {
            if (face == null) return false;

            BoundingBoxUV bbox = face.GetBoundingBox();
            int samples = 5;
            int verticalSamples = 0;
            int totalSamples = 0;

            for (int i = 0; i < samples; i++)
            {
                for (int j = 0; j < samples; j++)
                {
                    double u = bbox.Min.U + (bbox.Max.U - bbox.Min.U) * i / (samples - 1);
                    double v = bbox.Min.V + (bbox.Max.V - bbox.Min.V) * j / (samples - 1);

                    try
                    {
                        XYZ normal = face.ComputeNormal(new UV(u, v));

                        // Check if normal is near-horizontal (vertical face)
                        if (Math.Abs(normal.Z) < 0.2)
                            verticalSamples++;

                        totalSamples++;
                    }
                    catch
                    {
                        // Skip invalid UV coordinates
                    }
                }
            }

            return totalSamples > 0 && (double)verticalSamples / totalSamples > 0.7;
        }

        /// <summary>
        /// Ensures curve endpoints are ordered consistently (higher Z first)
        /// For curved edges, we sample midpoint to determine orientation
        /// </summary>
        private Curve NormalizeCurveOrientation(Curve curve)
        {
            XYZ start = curve.GetEndPoint(0);
            XYZ end = curve.GetEndPoint(1);

            // Sample midpoint to help determine orientation
            XYZ mid = curve.Evaluate(0.5, true);

            // Determine which end is "higher" based on average Z
            double avgStartZ = (start.Z + mid.Z) / 2;
            double avgEndZ = (end.Z + mid.Z) / 2;

            if (avgStartZ >= avgEndZ)
                return curve; // Already oriented correctly

            // Reverse the curve
            return curve.CreateReversed();
        }
    }
}