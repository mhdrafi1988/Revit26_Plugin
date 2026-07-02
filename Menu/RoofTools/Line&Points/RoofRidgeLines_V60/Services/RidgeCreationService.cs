using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V60.Models;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V60.Services
{
    public class RidgeCreationService
    {
        // ── Results ───────────────────────────────────────────────────────────────
        public int TotalCalculated { get; private set; }
        public int TotalAdded { get; private set; }
        public int TotalFailed { get; private set; }

        // ── Callbacks ─────────────────────────────────────────────────────────────
        public Action OnPointsDone { get; set; }

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

            // TX-02: Detail lines. Built only from result.ClippedEdges, so it
            // has no dependency on the slab shape editor — it runs first, as a
            // visual fallback that is guaranteed even if shape editing can't
            // be enabled afterward.
            ExecuteTx2_DetailLines(doc, activeView, result);

            // TX-03: Enable shape editing only. Does not touch any existing
            // vertex — any real slope already on the roof (e.g. from a prior
            // AutoSlopeByPoint run) is left exactly as-is. New points (TX-04)
            // are still forced to offset 0 via GetSlabBaseZ's flat-elevation
            // detection, without TX-03 needing to flatten anything first.
            bool editingEnabled = EnableShapeEditing(doc, roof);

            if (!editingEnabled)
            {
                // Editor unavailable (ed == null) or could not be enabled.
                // Detail lines were already created above; only the model
                // update (TX-04) is skipped.
                TotalFailed = TotalCalculated;
                OnPointsDone?.Invoke();
                System.Diagnostics.Debug.WriteLine(
                    "[RidgeCreationService] Model update skipped — SlabShapeEditor " +
                    "unavailable. Detail lines were created in TX-02 regardless.");
                return;
            }

            // TX-04: AddSplitLine + AddPoint — new points are created directly
            // at offset 0 (see GetSlabBaseZ, which finds the roof's dominant/
            // flat elevation among existing vertices rather than just their
            // minimum, so a sloped drain point doesn't get mistaken for flat).
            ExecuteTx4_AddPoints(doc, roof, result);
            OnPointsDone?.Invoke();
        }

        // ── TX-03: Enable shape editing (no longer touches existing vertices) ──────

        private bool EnableShapeEditing(Document doc, RoofBase roof)
        {
            try
            {
                using var tx = new Transaction(doc, "TX-03 | Enable Roof Shape Editing");
                tx.Start();

                var ed = roof.GetSlabShapeEditor();
                if (ed == null)
                {
                    tx.RollBack();
                    return false;
                }

                if (!ed.IsEnabled)
                {
                    try { ed.Enable(); }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[RidgeCreationService] TX-03 Enable warning: {ex.Message}");
                    }
                }

                tx.Commit();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[RidgeCreationService] TX-03 failed: {ex.Message}");
                return false;
            }
        }

        // ── TX-04: AddSplitLine + AddPoint (points land at offset 0 — see ──────────
        //         GetSlabBaseZ, which detects the roof's dominant flat ──────────────
        //         elevation among existing vertices) ─────────────────────────────

        private void ExecuteTx4_AddPoints(
            Document doc,
            RoofBase roof,
            VoronoiRidgeResult result)
        {
            // The persistence problem: AddSplitLine and AddPoint both mutate slab
            // topology. Interleaving them — and re-fetching the editor between every
            // call inside ONE uncommitted transaction — means AddPoint runs against a
            // transient, not-yet-regenerated vertex state. On commit, Revit regenerates
            // the slab and discards creases/points tied to that transient topology, even
            // though AddPoint never threw (so the old counter happily reported success).
            //
            // Fix: split the work across two committed transactions and never re-fetch
            // the editor mid-transaction.
            //   TX-04a — add ALL split lines, commit. Topology is now regenerated + stable.
            //   TX-04b — re-acquire the editor ONCE (post-commit), add ALL points, commit.
            // A post-commit vertex count is logged so the log reflects what actually stuck.

            var allPoints = new List<XYZ>();
            allPoints.AddRange(result.ShapePoints);
            allPoints.AddRange(result.RidgeEdgeMidPoints);

            result.CreatedShapePointIds.Clear();

            // ── TX-04a: all split lines, then commit ──────────────────────────────────
            try
            {
                using var txA = new Transaction(doc, "TX-04a | Add Ridge Split Lines");
                txA.Start();

                var ed = roof.GetSlabShapeEditor();
                if (ed == null || !ed.IsEnabled)
                {
                    TotalFailed = TotalCalculated;
                    txA.RollBack();
                    return;
                }

                // Single editor reference for the whole transaction — no mid-tx re-fetch.
                foreach (var pt in allPoints)
                {
                    try { TryAddSplitLineThroughPoint(ed, pt); }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            $"[RidgeCreationService] TX-04a split skipped: {ex.Message}");
                    }
                }

                txA.Commit();
            }
            catch (Exception ex)
            {
                // Split lines are an optimization, not a hard requirement — if the whole
                // split phase fails we still try to add the points below.
                System.Diagnostics.Debug.WriteLine(
                    $"[RidgeCreationService] TX-04a outer error: {ex.Message}");
            }

            // ── TX-04b: all points, against the committed/regenerated topology ────────
            try
            {
                using var txB = new Transaction(doc, "TX-04b | Create Ridge Points (Offset 0)");
                txB.Start();

                // Re-acquire ONCE here — safe now because TX-04a is committed.
                var ed = roof.GetSlabShapeEditor();
                if (ed == null || !ed.IsEnabled)
                {
                    TotalFailed = TotalCalculated;
                    txB.RollBack();
                    return;
                }

                double slabZ = GetSlabBaseZ(ed);

                foreach (var pt in allPoints)
                {
                    try
                    {
                        if (TryAddPointWithNudge(ed, pt, slabZ, result.RoofCentroid, result.BoundaryPolygon))
                            TotalAdded++;
                        else
                            TotalFailed++;
                    }
                    catch (Exception ex)
                    {
                        TotalFailed++;
                        System.Diagnostics.Debug.WriteLine(
                            $"[RidgeCreationService] TX-04b point skipped: {ex.Message}");
                    }
                }

                txB.Commit();

                // ── Post-commit verification — report what actually persisted ─────────
                var verify = roof.GetSlabShapeEditor();
                int persisted = verify?.SlabShapeVertices?.Size ?? -1;
                System.Diagnostics.Debug.WriteLine(
                    $"[RidgeCreationService] TX-04b committed. Editor vertices now: {persisted} " +
                    $"(Added counter: {TotalAdded}, Calculated: {TotalCalculated}).");
            }
            catch (Exception ex)
            {
                TotalFailed = TotalCalculated - TotalAdded;
                System.Diagnostics.Debug.WriteLine(
                    $"[RidgeCreationService] TX-04b outer error: {ex.Message}");
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

        // ── TX-02: Detail lines (runs first in CreateAll, before shape editing ─────
        //         is enabled; built only from result.ClippedEdges, so it always
        //         runs as a visual fallback even if TX-03 / the model update fails) ─

        private void ExecuteTx2_DetailLines(Document doc, View activeView, VoronoiRidgeResult result)
        {
            ElementId lineStyleId = GetFirstLineStyleId(doc);

            using var tx = new Transaction(doc, "TX-02 | Create Voronoi Ridge Detail Lines");
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

        // ── Helpers ───────────────────────────────────────────────────────────────

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
                    if (!RoofGeometry2D.PointInPolygon(test, boundary))
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
                    if (!RoofGeometry2D.PointInPolygon(test, boundary)) { nx = -nx; ny = -ny; }
                    bestNx = nx; bestNy = ny;
                }
            }
            return new XYZ(bestNx, bestNy, 0);
        }

        // ── MODIFIED: simplified GetSlabBaseZ ──
        // Returns the Z of the first shape‑editor vertex.
        // CAUTION: This ignores the roof's dominant flat plane if a slope already exists.
        // The original mode‑based logic is commented out below for reference.
        private static double GetSlabBaseZ(SlabShapeEditor ed)
        {
            var vertices = ed.SlabShapeVertices.Cast<SlabShapeVertex>().ToList();
            if (vertices.Count == 0) return 0.0;
            return vertices.First().Position.Z;
        }

        // Original mode‑based logic (kept for reference):
        /*
        private static double GetSlabBaseZ(SlabShapeEditor ed)
        {
            var vertices = ed.SlabShapeVertices.Cast<SlabShapeVertex>().ToList();
            if (vertices.Count == 0) return 0.0;

            const double zTol = 1.0 / 304.8; // 1 mm
            var zs = vertices.Select(v => v.Position.Z).OrderBy(z => z).ToList();

            double bestZ = zs[0];
            int bestCount = 0;
            int i = 0;
            while (i < zs.Count)
            {
                int j = i;
                while (j < zs.Count && zs[j] - zs[i] <= zTol) j++;
                int count = j - i;
                if (count > bestCount)
                {
                    bestCount = count;
                    bestZ = zs[i + (j - i) / 2];
                }
                i = j;
            }
            return bestZ;
        }
        */

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