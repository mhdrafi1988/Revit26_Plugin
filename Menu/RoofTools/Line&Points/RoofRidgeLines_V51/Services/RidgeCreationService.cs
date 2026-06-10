using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V51.Models;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V51.Services
{
    /// <summary>
    /// Correct transaction order:
    ///   TX-01 → Detail Lines (view-based, must come first so the view is not yet modified by shape editing)
    ///   TX-02 → SlabShapeEditor AddPoint on the Roof element (Revit 2026 API)
    ///
    /// Why this order?
    ///   Revit's SlabShapeEditor.Enable() triggers a model regeneration that can invalidate
    ///   the active view's element table.  Creating detail lines BEFORE enabling shape editing
    ///   avoids a potential "view out of date" exception and matches Revit's recommended
    ///   practice: view-only elements first, model geometry second.
    ///
    /// Shape-point Z level:
    ///   AddPoint(XYZ) is called with Z = baseElevation (bounding-box Min.Z of the roof).
    ///   The SlabShapeEditor ignores the Z value on input and resets every new vertex to the
    ///   slab's base elevation automatically, but we pass the correct Z anyway so the intent
    ///   is explicit and the point is never left at world zero (Z=0) by accident.
    /// </summary>
    public class RidgeCreationService
    {
        public void CreateAll(
            Document doc,
            View activeView,
            RoofBase roof,
            VoronoiRidgeResult result)
        {
            // ── TX-01 first: view elements (detail lines) ─────────────────────────
            ExecuteTx01_DetailLines(doc, activeView, result);

            // ── TX-02 second: model geometry (slab shape points) ──────────────────
            ExecuteTx02_ShapePoints(doc, roof, result);
        }

        // ── TX-01 ─────────────────────────────────────────────────────────────────

        private void ExecuteTx01_DetailLines(
            Document doc,
            View activeView,
            VoronoiRidgeResult result)
        {
            ElementId lineStyleId = GetFirstLineStyleId(doc);

            using (var tx = new Transaction(doc, "TX-01 | Create Voronoi Ridge Detail Lines"))
            {
                tx.Start();

                var ogs = new OverrideGraphicSettings();
                ogs.SetProjectionLineColor(new Color(255, 0, 0));
                ogs.SetProjectionLineWeight(3);

                result.CreatedDetailLineIds.Clear();

                foreach (var edge in result.ClippedEdges)
                {
                    try
                    {
                        if (edge.Start.DistanceTo(edge.End) < 1.0 / 304.8) continue;

                        Line line = Line.CreateBound(edge.Start, edge.End);
                        DetailLine dl = doc.Create.NewDetailCurve(activeView, line) as DetailLine;
                        if (dl == null) continue;

                        if (lineStyleId != ElementId.InvalidElementId)
                            dl.LineStyle = doc.GetElement(lineStyleId) as GraphicsStyle;

                        activeView.SetElementOverrides(dl.Id, ogs);
                        result.CreatedDetailLineIds.Add(dl.Id);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[RidgeCreationService] TX-01 line skipped: {ex.Message}");
                    }
                }

                tx.Commit();
            }
        }

        // ── TX-02 ─────────────────────────────────────────────────────────────────

        private void ExecuteTx02_ShapePoints(
            Document doc,
            RoofBase roof,
            VoronoiRidgeResult result)
        {
            using (var tx = new Transaction(doc, "TX-02 | Create Voronoi Ridge Shape Points on Roof"))
            {
                tx.Start();

                var editor = roof.GetSlabShapeEditor();
                if (editor == null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[RidgeCreationService] TX-02: GetSlabShapeEditor() returned null. " +
                        "Roof shape editing may not be available.");
                    tx.Commit();
                    return;
                }

                if (!editor.IsEnabled)
                    editor.Enable();

                // Use the roof's actual base elevation so points are never placed at Z = 0.
                // The SlabShapeEditor will reset the vertex Z to the slab surface during
                // regeneration, but passing the correct Z makes intent explicit and avoids
                // a world-origin placement if the regeneration is deferred.
                double baseZ = GetElementBaseElevation(roof);
                result.CreatedShapePointIds.Clear();

                foreach (var pt in result.ShapePoints)
                {
                    try
                    {
                        // Correct API in Revit 2026: AddPoint(XYZ)
                        // Z is set to baseZ (not 0) — see class-level comment.
                        SlabShapeVertex vertex = editor.AddPoint(new XYZ(pt.X, pt.Y, baseZ));
                        if (vertex != null)
                        {
                            // SlabShapeVertex does not expose ElementId in the 2026 API.
                            // Tracking is deferred; CreatedShapePointIds count stays at 0
                            // unless a future API version surfaces an Id.
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[RidgeCreationService] TX-02 point skipped: {ex.Message}");
                    }
                }

                tx.Commit();
            }
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static ElementId GetFirstLineStyleId(Document doc)
        {
            foreach (GraphicsStyle gs in new FilteredElementCollector(doc).OfClass(typeof(GraphicsStyle)))
            {
                if (gs.GraphicsStyleCategory?.Parent != null &&
                    gs.GraphicsStyleType == GraphicsStyleType.Projection)
                    return gs.Id;
            }
            return ElementId.InvalidElementId;
        }

        private static double GetElementBaseElevation(Element el)
        {
            BoundingBoxXYZ bb = el.get_BoundingBox(null);
            return bb != null ? bb.Min.Z : 0.0;
        }
    }
}