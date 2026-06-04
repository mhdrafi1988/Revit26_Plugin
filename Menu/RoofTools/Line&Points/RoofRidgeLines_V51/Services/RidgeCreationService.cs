using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V51.Models;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V51.Services
{
    /// <summary>
    /// TX-01 → SlabShapeEditor directly on the Roof element (Revit 2026 API).
    /// TX-02 → Detail Lines, first available LineStyle, red per-element view override.
    /// </summary>
    public class RidgeCreationService
    {
        public void CreateAll(
            Document doc,
            View activeView,
            RoofBase roof,
            VoronoiRidgeResult result)
        {
            ExecuteTx01_ShapePoints(doc, roof, result);
            ExecuteTx02_DetailLines(doc, activeView, result);
        }

        // ── TX-01 ─────────────────────────────────────────────────────────────────

        private void ExecuteTx01_ShapePoints(
            Document doc,
            RoofBase roof,
            VoronoiRidgeResult result)
        {
            using (var tx = new Transaction(doc, "TX-01 | Create Voronoi Ridge Shape Points on Roof"))
            {
                tx.Start();

                var editor = roof.GetSlabShapeEditor();
                if (editor == null)
                {
                    System.Diagnostics.Debug.WriteLine(
                        "[RidgeCreationService] TX-01: GetSlabShapeEditor() returned null. " +
                        "Roof shape editing may not be available.");
                    tx.Commit();
                    return;
                }

                if (!editor.IsEnabled)
                    editor.Enable();

                double baseZ = GetElementBaseElevation(roof);
                result.CreatedShapePointIds.Clear();

                foreach (var pt in result.ShapePoints)
                {
                    try
                    {
                        // AddPoint is the correct API in Revit 2026
                        SlabShapeVertex vertex = editor.AddPoint(new XYZ(pt.X, pt.Y, baseZ));
                        if (vertex != null)
                        {
                            // Note: SlabShapeVertex does not expose ElementId.
                            // If you need to track created points, you might store the vertex object or a hash.
                            // For now, we skip adding to CreatedShapePointIds.
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[RidgeCreationService] TX-01 point skipped: {ex.Message}");
                    }
                }

                tx.Commit();
            }
        }

        // ── TX-02 ─────────────────────────────────────────────────────────────────

        private void ExecuteTx02_DetailLines(
            Document doc,
            View activeView,
            VoronoiRidgeResult result)
        {
            ElementId lineStyleId = GetFirstLineStyleId(doc);

            using (var tx = new Transaction(doc, "TX-02 | Create Voronoi Ridge Detail Lines"))
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
                            $"[RidgeCreationService] TX-02 line skipped: {ex.Message}");
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