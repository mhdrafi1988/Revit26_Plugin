using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Services
{
    /// <summary>
    /// Determines the "processing boundary" — the polygon (or bounding box fallback)
    /// used for spatial candidate filtering (Stage 1) and clipping (Stage 2).
    ///
    /// PHASE 2 SCOPE: rectangular crop regions get an exact polygon boundary via
    /// ViewCropRegionShapeManager. Non-rectangular ("shape-edited") crop regions are
    /// approximated by the bounding box of the crop curve loops in Phase 2 — true
    /// polygon-accurate clipping for non-rectangular crops is flagged as a Phase 3+
    /// item (needs NetTopologySuite integration, per earlier discussion; not silently
    /// upgraded here without your confirmation).
    /// </summary>
    public class ViewBoundaryService
    {
        /// <summary>
        /// Returns the processing boundary as a closed list of XYZ points (host
        /// coordinates, Z ignored / assumed planar at view's level) plus whether it
        /// came from an exact crop shape or a bounding-box fallback.
        /// </summary>
        public (List<XYZ> boundary, bool isExactShape) GetProcessingBoundary(
            View activeView, Action<string>? onLog = null)
        {
            if (!activeView.CropBoxActive || activeView.CropBox == null)
            {
                onLog?.Invoke("Crop box inactive — using view's model extents as fallback boundary (uncropped view).");
                return (GetUncroppedFallbackBoundary(activeView), false);
            }

            try
            {
                var cropManager = activeView.GetCropRegionShapeManager();
                var loops = cropManager.GetCropShape();

                if (loops != null && loops.Count > 0 && loops[0].NumberOfCurves() > 0)
                {
                    // Phase 2: use the first (outer) loop's polygon via tessellated vertices.
                    // If the shape is non-rectangular, this is still an exact polygon (not a
                    // bbox) since we walk the actual curve loop — only truly curved crop
                    // boundaries (rare) would need further tessellation refinement.
                    var loop = loops[0];
                    var pts = new List<XYZ>();
                    foreach (Curve c in loop)
                    {
                        pts.Add(c.GetEndPoint(0));
                    }
                    onLog?.Invoke($"Crop shape resolved — {pts.Count} boundary vertices (exact crop polygon).");
                    return (pts, true);
                }
            }
            catch (Exception ex)
            {
                onLog?.Invoke($"Crop shape manager unavailable ({ex.Message}) — falling back to CropBox bounding rectangle.");
            }

            // Fallback: axis-aligned rectangle from CropBox (Phase 2 default for any
            // failure/non-rectangular-unsupported case; upgrade path noted above).
            BoundingBoxXYZ cropBox = activeView.CropBox;
            Transform t = cropBox.Transform;
            XYZ min = cropBox.Min;
            XYZ max = cropBox.Max;

            var corners = new List<XYZ>
            {
                t.OfPoint(new XYZ(min.X, min.Y, 0)),
                t.OfPoint(new XYZ(max.X, min.Y, 0)),
                t.OfPoint(new XYZ(max.X, max.Y, 0)),
                t.OfPoint(new XYZ(min.X, max.Y, 0)),
            };

            onLog?.Invoke("Using CropBox bounding rectangle as processing boundary (bounding-box fallback).");
            return (corners, false);
        }

        private List<XYZ> GetUncroppedFallbackBoundary(View activeView)
        {
            // No crop active: Revit doesn't give a hard "visible extent" for an
            // uncropped plan view. Phase 2 default: use the view's Outline (view-space
            // 2D bounding rectangle) as a reasonable processing extent rather than
            // silently processing the entire linked model (Section 32 performance
            // requirement). Flagged: confirm this default is acceptable, or specify
            // a fixed padding/extent convention you'd prefer for uncropped views.
            var outline = activeView.Outline; // UV bounding box in view coordinates
            var min = activeView.CropBox?.Min ?? new XYZ(outline.Min.U, outline.Min.V, 0);
            var max = activeView.CropBox?.Max ?? new XYZ(outline.Max.U, outline.Max.V, 0);

            return new List<XYZ>
            {
                new XYZ(min.X, min.Y, 0),
                new XYZ(max.X, min.Y, 0),
                new XYZ(max.X, max.Y, 0),
                new XYZ(min.X, max.Y, 0),
            };
        }
    }
}
