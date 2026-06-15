using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V53.Models;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V53.Services
{
    public class RidgeCreationService
    {
        // ── Results ───────────────────────────────────────────────────────────────
        public string FirstResetStatus { get; private set; } = "N/A";
        public string LastResetStatus { get; private set; } = "N/A";
        public int TotalCalculated { get; private set; }
        public int TotalAdded { get; private set; }
        public int TotalFailed { get; private set; }

        // ── Callbacks ─────────────────────────────────────────────────────────────
        public Action OnFirstResetDone { get; set; }
        public Action OnPointsDone { get; set; }
        public Action OnLastResetDone { get; set; }

        // ─────────────────────────────────────────────────────────────────────────

        public void CreateAll(
            Document doc,
            View activeView,
            RoofBase roof,
            VoronoiRidgeResult result)
        {
            TotalCalculated = result.ShapePoints.Count + result.RidgeEdgeMidPoints.Count;
            TotalAdded = 0;
            TotalFailed = 0;

            FirstResetStatus = "N/A";
            OnFirstResetDone?.Invoke();

            // TX-0: Enable shape editing
            if (!EnableShapeEditing(doc, roof))
            {
                LastResetStatus = "Skipped — editor unavailable";
                OnPointsDone?.Invoke();
                OnLastResetDone?.Invoke();
                ExecuteTx_DetailLines(doc, activeView, result);
                return;
            }

            // Capture existing vertices BEFORE adding new points
            var existingVertices = GetCurrentVertexPositions(roof);

            // TX-1: AddSplitLine + AddPoint
            ExecuteTx1_AddPoints(doc, roof, result);
            OnPointsDone?.Invoke();

            // TX-2: Zero ONLY the newly added vertices (set offset = 0)
            ExecuteTx2_ZeroNewVertices(doc, roof, existingVertices);
            OnLastResetDone?.Invoke();

            // TX-3: Detail lines
            ExecuteTx_DetailLines(doc, activeView, result);
        }

        // ── TX-0: Enable ──────────────────────────────────────────────────────────

        private static bool EnableShapeEditing(Document doc, RoofBase roof)
        {
            try
            {
                using var tx = new Transaction(doc, "TX-0 | Enable Roof Shape Editing");
                tx.Start();
                var ed = roof.GetSlabShapeEditor();
                if (ed == null) { tx.RollBack(); return false; }
                if (!ed.IsEnabled)
                {
                    try { ed.Enable(); }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[RidgeCreationService] TX-0 Enable warning: {ex.Message}");
                    }
                }
                tx.Commit();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[RidgeCreationService] TX-0 failed: {ex.Message}");
                return false;
            }
        }

        // ── Capture existing vertex positions (2D) ─────────────────────────────────

        private static List<XYZ> GetCurrentVertexPositions(RoofBase roof)
        {
            var ed = roof.GetSlabShapeEditor();
            if (ed == null || !ed.IsEnabled) return new List<XYZ>();

            return ed.SlabShapeVertices
                .Cast<SlabShapeVertex>()
                .Select(v => v.Position)
                .Where(p => p != null)
                .Select(p => new XYZ(p.X, p.Y, 0))
                .ToList();
        }

        // ── TX-1: AddSplitLine + AddPoint (unchanged except for tracking) ─────────

        private void ExecuteTx1_AddPoints(
            Document doc,
            RoofBase roof,
            VoronoiRidgeResult result)
        {
            try
            {
                using var tx = new Transaction(doc, "TX-1 | Add Voronoi Shape Points");
                tx.Start();

                var ed = roof.GetSlabShapeEditor();
                if (ed == null || !ed.IsEnabled)
                {
                    TotalFailed = TotalCalculated;
                    tx.Commit();
                    return;
                }

                result.CreatedShapePointIds.Clear();

                foreach (var pt in result.ShapePoints)
                {
                    try
                    {
                        // Step 1: cut a crease through the interior XY
                        TryAddSplitLineThroughPoint(ed, pt);

                        // Step 2: re-fetch so crease vertices are visible
                        ed = roof.GetSlabShapeEditor();
                        if (ed == null || !ed.IsEnabled) { TotalFailed++; continue; }

                        // Step 3: add the point; nudge toward centroid if outside slab face
                        double slabZ = GetSlabBaseZ(ed);
                        if (TryAddPointWithNudge(ed, pt, slabZ, result.RoofCentroid, result.BoundaryPolygon))
                            TotalAdded++;
                        else
                        {
                            TotalFailed++;
                            continue;
                        }
                    }
                    catch (Exception ex)
                    {
                        TotalFailed++;
                        System.Diagnostics.Debug.WriteLine(
                            $"[RidgeCreationService] TX-1 point skipped: {ex.Message}");
                    }
                }

                // ── Mid-points: one per clipped ridge edge ────────────────────────
                foreach (var pt in result.RidgeEdgeMidPoints)
                {
                    try
                    {
                        TryAddSplitLineThroughPoint(ed, pt);
                        ed = roof.GetSlabShapeEditor();
                        if (ed == null || !ed.IsEnabled) { TotalFailed++; continue; }

                        double slabZ = GetSlabBaseZ(ed);
                        if (TryAddPointWithNudge(ed, pt, slabZ, result.RoofCentroid, result.BoundaryPolygon))
                            TotalAdded++;
                        else
                            TotalFailed++;
                    }
                    catch (Exception ex)
                    {
                        TotalFailed++;
                        System.Diagnostics.Debug.WriteLine(
                            $"[RidgeCreationService] TX-1 mid-point skipped: {ex.Message}");
                    }
                }

                tx.Commit();
            }
            catch (Exception ex)
            {
                TotalFailed = TotalCalculated - TotalAdded;
                System.Diagnostics.Debug.WriteLine(
                    $"[RidgeCreationService] TX-1 outer error: {ex.Message}");
            }
        }

        // ── TX-2: Zero only new vertices (offset = 0) ──────────────────────────────

        private void ExecuteTx2_ZeroNewVertices(
            Document doc,
            RoofBase roof,
            List<XYZ> existingVertices)
        {
            try
            {
                using var tx = new Transaction(doc, "TX-2 | Zero New Shape Point Offsets");
                tx.Start();

                var ed = roof.GetSlabShapeEditor();
                if (ed == null || !ed.IsEnabled)
                {
                    LastResetStatus = "Skipped — editor unavailable";
                    tx.Commit();
                    return;
                }

                // Get all current vertices after TX-1
                var allVertices = ed.SlabShapeVertices
                    .Cast<SlabShapeVertex>()
                    .ToList();

                int zeroed = 0;
                const double snapTol = 1.0 / 304.8; // 1 mm tolerance for matching

                foreach (var v in allVertices)
                {
                    XYZ pos = v.Position;
                    if (pos == null) continue;

                    // Check if this vertex existed before TX-1
                    bool existed = existingVertices.Any(ex =>
                        Math.Abs(ex.X - pos.X) < snapTol &&
                        Math.Abs(ex.Y - pos.Y) < snapTol);

                    if (!existed)
                    {
                        try
                        {
                            // Set offset to 0 (relative to roof base level)
                            ed.ModifySubElement(v, 0.0);
                            zeroed++;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"[RidgeCreationService] TX-2 ModifySubElement failed: {ex.Message}");
                        }
                    }
                }

                LastResetStatus = $"Success ({zeroed} new vertices zeroed)";
                tx.Commit();
            }
            catch (Exception ex)
            {
                LastResetStatus = $"Failed — {ex.Message}";
            }
        }

        // ── AddSplitLine helper (unchanged) ───────────────────────────────────────

        private static void TryAddSplitLineThroughPoint(SlabShapeEditor ed, XYZ pt)
        {
            var vertices = ed.SlabShapeVertices.Cast<SlabShapeVertex>().ToList();
            if (vertices.Count < 2) return;

            SlabShapeVertex leftV = null;
            SlabShapeVertex rightV = null;
            double bestL = double.MaxValue;
            double bestR = double.MaxValue;

            foreach (var v in vertices)
            {
                XYZ p = v.Position;
                if (p == null) continue;

                double dx = p.X - pt.X;
                double dy = p.Y - pt.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < 1e-6) continue;

                // Only vertices within ±45° horizontal cone (|dy| < |dx|)
                if (Math.Abs(dy) >= Math.Abs(dx)) continue;

                if (dx < 0 && dist < bestL) { bestL = dist; leftV = v; }
                if (dx > 0 && dist < bestR) { bestR = dist; rightV = v; }
            }

            if (leftV == null || rightV == null) return;

            try { ed.AddSplitLine(leftV, rightV); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[RidgeCreationService] AddSplitLine skipped: {ex.Message}");
            }
        }

        // ── TX-3: Detail lines (unchanged) ────────────────────────────────────────

        private void ExecuteTx_DetailLines(Document doc, View activeView, VoronoiRidgeResult result)
        {
            ElementId lineStyleId = GetFirstLineStyleId(doc);

            using var tx = new Transaction(doc, "TX-3 | Create Voronoi Ridge Detail Lines");
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
                        $"[RidgeCreationService] TX-3 line skipped: {ex.Message}");
                }
            }

            tx.Commit();
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static double Dist2D(XYZ a, XYZ b)
            => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        // ── Phase-1: centroid nudge (convex / near-edge failures) ─────────────────
        // ── Phase-2: nearest-edge inset (concave / amoeba roofs) ─────────────────
        //
        // Strategy:
        //   Phase 1 — try the exact point, then nudge 5 % per step toward the
        //             boundary centroid (10 steps → up to 50 % travel).
        //             Works for points just outside a convex boundary or in a
        //             mesh-triangle gap.
        //   Phase 2 — if centroid nudging fails (centroid may itself be outside
        //             for C/L/amoeba shapes), find the nearest boundary edge,
        //             step inward from its midpoint at 5 mm intervals up to 50 mm.
        //             The inward normal is computed from the signed-area winding so
        //             it always points into the polygon regardless of shape.

        private static bool TryAddPointWithNudge(
            SlabShapeEditor ed,
            XYZ pt,
            double slabZ,
            XYZ centroid,
            List<XYZ> boundary)
        {
            // ── Phase 1: centroid nudge ───────────────────────────────────────────
            const int p1Steps = 10;
            const double p1Fraction = 0.05;

            for (int step = 0; step <= p1Steps; step++)
            {
                double t = step * p1Fraction;
                XYZ candidate = new XYZ(
                    pt.X + (centroid.X - pt.X) * t,
                    pt.Y + (centroid.Y - pt.Y) * t,
                    slabZ);
                try
                {
                    ed.AddPoint(candidate);
                    if (step > 0)
                        System.Diagnostics.Debug.WriteLine(
                            $"[RidgeCreationService] Phase-1 accepted after {step} nudge(s) " +
                            $"({t * 100:F0}% toward centroid)");
                    return true;
                }
                catch { /* keep nudging */ }
            }

            // ── Phase 2: nearest-edge inset ───────────────────────────────────────
            if (boundary == null || boundary.Count < 3)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[RidgeCreationService] Phase-2 skipped — no boundary polygon.");
                return false;
            }

            XYZ insetOrigin = GetNearestEdgeInsetPoint(pt, boundary, insetMm: 5.0);
            if (insetOrigin == null)
            {
                System.Diagnostics.Debug.WriteLine(
                    "[RidgeCreationService] Phase-2 skipped — could not compute inset point.");
                return false;
            }

            const int p2Steps = 10;
            const double p2StepMm = 5.0; // walk further inward 5 mm per step

            for (int step = 0; step < p2Steps; step++)
            {
                double extraInset = step * p2StepMm / 304.8; // mm → ft
                // Direction from boundary midpoint inward (unit vector already baked in
                // GetNearestEdgeInsetPoint; we add more along the same direction)
                XYZ dir = GetNearestEdgeInwardNormal(pt, boundary);
                XYZ candidate = new XYZ(
                    insetOrigin.X + dir.X * extraInset,
                    insetOrigin.Y + dir.Y * extraInset,
                    slabZ);
                try
                {
                    ed.AddPoint(candidate);
                    System.Diagnostics.Debug.WriteLine(
                        $"[RidgeCreationService] Phase-2 accepted at step {step} " +
                        $"(inset {5 + step * 5} mm from nearest edge)");
                    return true;
                }
                catch { /* keep stepping */ }
            }

            System.Diagnostics.Debug.WriteLine(
                "[RidgeCreationService] Both phases failed — point skipped.");
            return false;
        }

        // Returns the midpoint of the nearest boundary edge offset inward by insetMm.
        private static XYZ GetNearestEdgeInsetPoint(XYZ pt, List<XYZ> boundary, double insetMm)
        {
            double insetFt = insetMm / 304.8;
            int n = boundary.Count;
            double bestDist = double.MaxValue;
            XYZ bestPoint = null;

            for (int i = 0; i < n; i++)
            {
                XYZ a = boundary[i];
                XYZ b = boundary[(i + 1) % n];
                XYZ mid = new XYZ((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0, 0);
                double dist = Math.Sqrt(Math.Pow(pt.X - mid.X, 2) + Math.Pow(pt.Y - mid.Y, 2));
                if (dist < bestDist)
                {
                    bestDist = dist;
                    // Edge tangent → inward normal (rotate 90°; sign from polygon winding)
                    double ex = b.X - a.X, ey = b.Y - a.Y;
                    double len = Math.Sqrt(ex * ex + ey * ey);
                    if (len < 1e-9) continue;
                    // For CCW winding the left normal (-ey, ex) points inward
                    double nx = -ey / len, ny = ex / len;
                    // Validate direction: nudged midpoint should be inside polygon
                    XYZ test = new XYZ(mid.X + nx * insetFt, mid.Y + ny * insetFt, 0);
                    if (!PointInPolygon2D(test, boundary))
                    { nx = -nx; ny = -ny; } // flip for CW winding
                    bestPoint = new XYZ(mid.X + nx * insetFt, mid.Y + ny * insetFt, 0);
                }
            }
            return bestPoint;
        }

        // Returns the inward unit normal of the nearest boundary edge (used for Phase-2 stepping).
        private static XYZ GetNearestEdgeInwardNormal(XYZ pt, List<XYZ> boundary)
        {
            int n = boundary.Count;
            double bestDist = double.MaxValue;
            double bestNx = 0, bestNy = 0;

            for (int i = 0; i < n; i++)
            {
                XYZ a = boundary[i];
                XYZ b = boundary[(i + 1) % n];
                XYZ mid = new XYZ((a.X + b.X) / 2.0, (a.Y + b.Y) / 2.0, 0);
                double dist = Math.Sqrt(Math.Pow(pt.X - mid.X, 2) + Math.Pow(pt.Y - mid.Y, 2));
                if (dist < bestDist)
                {
                    bestDist = dist;
                    double ex = b.X - a.X, ey = b.Y - a.Y;
                    double len = Math.Sqrt(ex * ex + ey * ey);
                    if (len < 1e-9) continue;
                    double nx = -ey / len, ny = ex / len;
                    XYZ test = new XYZ(mid.X + nx / 304.8, mid.Y + ny / 304.8, 0);
                    if (!PointInPolygon2D(test, boundary)) { nx = -nx; ny = -ny; }
                    bestNx = nx; bestNy = ny;
                }
            }
            return new XYZ(bestNx, bestNy, 0);
        }

        // Ray-casting point-in-polygon (works for any simple polygon including amoeba).
        private static bool PointInPolygon2D(XYZ pt, List<XYZ> polygon)
        {
            int n = polygon.Count;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                double xi = polygon[i].X, yi = polygon[i].Y;
                double xj = polygon[j].X, yj = polygon[j].Y;
                if (((yi > pt.Y) != (yj > pt.Y)) &&
                    (pt.X < (xj - xi) * (pt.Y - yi) / (yj - yi) + xi))
                    inside = !inside;
            }
            return inside;
        }

        private static double GetSlabBaseZ(SlabShapeEditor ed)
        {
            var vertices = ed.SlabShapeVertices.Cast<SlabShapeVertex>().ToList();
            if (vertices.Count == 0) return 0.0;
            // All flat-slab corner vertices share the same base Z
            return vertices.Select(v => v.Position.Z).Min();
        }

        private static ElementId GetFirstLineStyleId(Document doc)
        {
            foreach (GraphicsStyle gs in new FilteredElementCollector(doc)
                         .OfClass(typeof(GraphicsStyle)))
            {
                if (gs.GraphicsStyleCategory?.Parent != null &&
                    gs.GraphicsStyleType == GraphicsStyleType.Projection)
                    return gs.Id;
            }
            return ElementId.InvalidElementId;
        }
    }
}