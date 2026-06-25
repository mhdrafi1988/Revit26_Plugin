using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V64.Models;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V64.Services
{
    public class RidgeCreationService
    {
        // ── Results ───────────────────────────────────────────────────────────────
        public int TotalCalculated { get; private set; }
        public int TotalAdded { get; private set; }
        public int TotalFailed { get; private set; }

        // ── Callbacks ─────────────────────────────────────────────────────────────
        public Action OnPointsDone { get; set; }

        /// <summary>
        /// Raised whenever a slab-geometry warning is caught by the TX-04
        /// failure preprocessor, or when points are removed by deduplication.
        /// The string is human-readable and suitable for the pipeline log.
        /// </summary>
        public Action<string> OnSlabWarning { get; set; }

        // ── Dedup tolerance ───────────────────────────────────────────────────────
        // Points closer than this (Revit feet) are considered duplicates before TX-04.
        // 50 mm — matches the default proximity-distance in the ViewModel.
        // Prevents degenerate mesh triangles on complex roofs (168+ boundary vertices).
        private const double DedupToleranceFt = 50.0 / 304.8;

        // ─────────────────────────────────────────────────────────────────────────

        public void CreateAll(
            Document doc,
            View activeView,
            RoofBase roof,
            VoronoiRidgeResult result)
        {
            // ── Pre-filter: deduplicate ShapePoints and RidgeEdgeMidPoints ────────
            //
            // On complex roofs (many boundary vertices, tightly-clustered drains)
            // the Voronoi + clipping pipeline can produce shape points that are very
            // close together. Adding all of them causes the SlabShapeEditor mesh
            // triangulator to generate degenerate zero-area faces, which triggers
            // Revit's "Slab Shape Edit failed" warning at TX-04 commit time even
            // though every individual AddPoint call succeeded.
            // Deduplicating to 50 mm removes excess points while keeping the
            // geometrically meaningful ones (first occurrence of each cluster wins).

            var dedupedShapePoints = DeduplicatePoints(result.ShapePoints, DedupToleranceFt);
            var dedupedMidPoints = DeduplicatePoints(result.RidgeEdgeMidPoints, DedupToleranceFt);

            int shapeDropped = result.ShapePoints.Count - dedupedShapePoints.Count;
            int midDropped = result.RidgeEdgeMidPoints.Count - dedupedMidPoints.Count;

            TotalCalculated = dedupedShapePoints.Count + dedupedMidPoints.Count;
            TotalAdded = 0;
            TotalFailed = 0;

            if (shapeDropped > 0 || midDropped > 0)
                OnSlabWarning?.Invoke(
                    $"[DEDUP] Removed {shapeDropped} ShapePoint(s) and {midDropped} MidPoint(s) " +
                    $"within {DedupToleranceFt * 304.8:F0} mm tolerance before TX-04.");

            // TX-02: Detail lines — runs first, no dependency on shape editor.
            ExecuteTx2_DetailLines(doc, activeView, result);

            // TX-03: Enable shape editing only. Does not touch existing vertices.
            bool editingEnabled = EnableShapeEditing(doc, roof);

            if (!editingEnabled)
            {
                TotalFailed = TotalCalculated;
                OnPointsDone?.Invoke();
                System.Diagnostics.Debug.WriteLine(
                    "[RidgeCreationService] Model update skipped — SlabShapeEditor " +
                    "unavailable. Detail lines were created in TX-02 regardless.");
                return;
            }

            // TX-04: AddSplitLine + AddPoint using the deduplicated point lists.
            // Inner-loop intersection points are already inside result.ShapePoints
            // (added by InnerLoopIntersectionService), so no separate loop is needed.
            ExecuteTx4_AddPoints(doc, roof, result, dedupedShapePoints, dedupedMidPoints);
            OnPointsDone?.Invoke();
        }

        // ── Deduplication ─────────────────────────────────────────────────────────
        //
        // Greedy pass — keeps the first occurrence of each spatial cluster.
        // ShapePoints is processed before RidgeEdgeMidPoints in CreateAll so Voronoi
        // vertices always win over mid-points when they are near-coincident.

        private static List<XYZ> DeduplicatePoints(List<XYZ> points, double toleranceFt)
        {
            var kept = new List<XYZ>(points.Count);
            foreach (var pt in points)
            {
                bool isDup = kept.Any(k =>
                    Math.Abs(k.X - pt.X) < toleranceFt &&
                    Math.Abs(k.Y - pt.Y) < toleranceFt &&
                    Math.Sqrt(Math.Pow(k.X - pt.X, 2) + Math.Pow(k.Y - pt.Y, 2)) < toleranceFt);

                if (!isDup) kept.Add(pt);
            }
            return kept;
        }

        // ── TX-03: Enable shape editing ───────────────────────────────────────────

        private bool EnableShapeEditing(Document doc, RoofBase roof)
        {
            try
            {
                using var tx = new Transaction(doc, "TX-03 | Enable Roof Shape Editing");
                tx.Start();

                var ed = roof.GetSlabShapeEditor();
                if (ed == null) { tx.RollBack(); return false; }

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

        // ── TX-04: AddSplitLine + AddPoint ────────────────────────────────────────
        //
        // Accepts deduplicated point lists so the mesh triangulator never receives
        // two points closer than DedupToleranceFt.
        //
        // A SlabShapePreprocessor is attached to the transaction so that any
        // slab-geometry warnings Revit raises at commit time are:
        //   • dismissed silently  (no blocking dialog shown to the user)
        //   • forwarded via OnSlabWarning → ViewModel → pipeline log (copyable)

        private void ExecuteTx4_AddPoints(
            Document doc,
            RoofBase roof,
            VoronoiRidgeResult result,
            List<XYZ> shapePoints,
            List<XYZ> midPoints)
        {
            try
            {
                using var tx = new Transaction(doc, "TX-04 | Create Ridge Points (Offset 0)");

                // ── Failure handling ──────────────────────────────────────────────
                var preprocessor = new SlabShapePreprocessor();
                var failOpts = tx.GetFailureHandlingOptions();
                failOpts.SetFailuresPreprocessor(preprocessor);
                failOpts.SetClearAfterRollback(true);
                tx.SetFailureHandlingOptions(failOpts);

                tx.Start();

                var ed = roof.GetSlabShapeEditor();
                if (ed == null || !ed.IsEnabled)
                {
                    TotalFailed = TotalCalculated;
                    tx.Commit();
                    return;
                }

                Face topFace = GetRoofTopFace(roof);
                result.CreatedShapePointIds.Clear();

                // ── ShapePoints (Voronoi vertices + inner-loop intersections) ─────
                foreach (var pt in shapePoints)
                {
                    try
                    {
                        XYZ checkedPt = EnsureOnTopFace(pt, topFace, result.BoundaryPolygon);
                        if (checkedPt == null)
                        {
                            TotalFailed++;
                            System.Diagnostics.Debug.WriteLine(
                                $"[RidgeCreationService] ShapePoint skipped — not on top face " +
                                $"after 3 snap attempts: X={pt.X * 304.8:F1} Y={pt.Y * 304.8:F1} mm");
                            continue;
                        }

                        TryAddSplitLineThroughPoint(ed, checkedPt);
                        ed = roof.GetSlabShapeEditor();
                        if (ed == null || !ed.IsEnabled) { TotalFailed++; continue; }

                        double slabZ = GetSlabBaseZ(ed);
                        if (TryAddPointWithNudge(ed, checkedPt, slabZ, result.RoofCentroid,
                                                 result.BoundaryPolygon, roof))
                            TotalAdded++;
                        else
                            TotalFailed++;
                    }
                    catch (Exception ex)
                    {
                        TotalFailed++;
                        System.Diagnostics.Debug.WriteLine(
                            $"[RidgeCreationService] TX-04 ShapePoint skipped: {ex.Message}");
                    }
                }

                // ── RidgeEdgeMidPoints ────────────────────────────────────────────
                foreach (var pt in midPoints)
                {
                    try
                    {
                        XYZ checkedPt = EnsureOnTopFace(pt, topFace, result.BoundaryPolygon);
                        if (checkedPt == null)
                        {
                            TotalFailed++;
                            System.Diagnostics.Debug.WriteLine(
                                $"[RidgeCreationService] MidPoint skipped — not on top face " +
                                $"after 3 snap attempts: X={pt.X * 304.8:F1} Y={pt.Y * 304.8:F1} mm");
                            continue;
                        }

                        TryAddSplitLineThroughPoint(ed, checkedPt);
                        ed = roof.GetSlabShapeEditor();
                        if (ed == null || !ed.IsEnabled) { TotalFailed++; continue; }

                        double slabZ = GetSlabBaseZ(ed);
                        if (TryAddPointWithNudge(ed, checkedPt, slabZ, result.RoofCentroid,
                                                 result.BoundaryPolygon, roof))
                            TotalAdded++;
                        else
                            TotalFailed++;
                    }
                    catch (Exception ex)
                    {
                        TotalFailed++;
                        System.Diagnostics.Debug.WriteLine(
                            $"[RidgeCreationService] TX-04 MidPoint skipped: {ex.Message}");
                    }
                }

                tx.Commit();

                // Forward any slab warnings collected during commit to the pipeline log
                foreach (var warning in preprocessor.CapturedWarnings)
                    OnSlabWarning?.Invoke(warning);
            }
            catch (Exception ex)
            {
                TotalFailed = TotalCalculated - TotalAdded;
                System.Diagnostics.Debug.WriteLine(
                    $"[RidgeCreationService] TX-04 outer error: {ex.Message}");
            }
        }

        // ── IFailuresPreprocessor — slab shape warning interceptor ────────────────
        //
        // Revit raises a Warning (not an Error) when the mesh triangulation produces
        // degenerate faces after AddPoint commits. This preprocessor:
        //   • calls DeleteWarning() on every Warning severity failure → no dialog
        //   • stores a human-readable description in CapturedWarnings
        //   • returns ProceedWithRollBack only if an actual Error is present
        //     (Errors cannot be silently dismissed)

        private sealed class SlabShapePreprocessor : IFailuresPreprocessor
        {
            public List<string> CapturedWarnings { get; } = new List<string>();

            public FailureProcessingResult PreprocessFailures(FailuresAccessor accessor)
            {
                var failures = accessor.GetFailureMessages().ToList();
                if (failures.Count == 0)
                    return FailureProcessingResult.Continue;

                bool hasErrors = false;

                foreach (var f in failures)
                {
                    string desc =
                        $"[SLAB WARNING] {f.GetDescriptionText()} " +
                        $"(severity: {f.GetSeverity()}" +
                        (f.GetFailingElementIds().Any()
                            ? $", elements: {string.Join(", ", f.GetFailingElementIds().Select(id => id.Value))}"
                            : "") +
                        ")";

                    CapturedWarnings.Add(desc);
                    System.Diagnostics.Debug.WriteLine($"[SlabShapePreprocessor] {desc}");

                    if (f.GetSeverity() == FailureSeverity.Warning)
                        accessor.DeleteWarning(f);
                    else
                        hasErrors = true;
                }

                return hasErrors
                    ? FailureProcessingResult.ProceedWithRollBack
                    : FailureProcessingResult.Continue;
            }
        }

        // ── Top-face check ────────────────────────────────────────────────────────

        private static XYZ EnsureOnTopFace(XYZ pt, Face topFace, List<XYZ> boundary)
        {
            if (IsOnTopFace(pt, topFace)) return pt;
            if (topFace == null || boundary == null || boundary.Count < 3) return null;

            for (int attempt = 1; attempt <= 3; attempt++)
            {
                double insetMm = attempt * 5.0;
                XYZ snapped = GetNearestEdgeInsetPoint(pt, boundary, insetMm);
                if (snapped != null && IsOnTopFace(snapped, topFace))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[RidgeCreationService] Top-face snap accepted at attempt {attempt} " +
                        $"({insetMm} mm inset): X={snapped.X * 304.8:F1} Y={snapped.Y * 304.8:F1} mm");
                    return snapped;
                }
            }

            return null;
        }

        private static bool IsOnTopFace(XYZ pt, Face topFace)
        {
            if (topFace == null) return true;
            try { return topFace.Project(pt) != null; }
            catch { return false; }
        }

        private static Face GetRoofTopFace(RoofBase roof)
        {
            try
            {
                var opts = new Options { ComputeReferences = false, IncludeNonVisibleObjects = false };
                GeometryElement geom = roof.get_Geometry(opts);
                if (geom == null) return null;

                Face best = null;
                double bestZ = double.MinValue;

                foreach (GeometryObject obj in geom)
                {
                    Solid solid = obj as Solid;
                    if (solid == null || solid.Faces.Size == 0) continue;

                    foreach (Face face in solid.Faces)
                    {
                        try
                        {
                            XYZ normal = face.ComputeNormal(new UV(0.5, 0.5));
                            if (normal.Z <= 0.1) continue;

                            BoundingBoxUV bb = face.GetBoundingBox();
                            UV mid = new UV((bb.Min.U + bb.Max.U) / 2.0,
                                                       (bb.Min.V + bb.Max.V) / 2.0);
                            XYZ centroid = face.Evaluate(mid);
                            if (centroid.Z > bestZ) { bestZ = centroid.Z; best = face; }
                        }
                        catch { /* skip degenerate face */ }
                    }
                }

                return best;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[RidgeCreationService] GetRoofTopFace failed: {ex.Message}");
                return null;
            }
        }

        // ── AddSplitLine helper ───────────────────────────────────────────────────

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

        // ── TryAddPointWithNudge ──────────────────────────────────────────────────

        private static bool TryAddPointWithNudge(
            SlabShapeEditor ed,
            XYZ pt,
            double slabZ,
            XYZ centroid,
            List<XYZ> boundary,
            RoofBase roof)
        {
            // Phase 1: centroid nudge (up to 50% travel in 10 steps of 5%)
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
                    var before = ed.SlabShapeVertices.Cast<SlabShapeVertex>()
                                   .Select(v => v.Position).ToList();
                    ed.AddPoint(candidate);
                    ZeroNewVertexOffset(ed, before, roof);
                    if (step > 0)
                        System.Diagnostics.Debug.WriteLine(
                            $"[RidgeCreationService] Phase-1 accepted after {step} nudge(s) " +
                            $"({t * 100:F0}% toward centroid)");
                    return true;
                }
                catch { /* keep nudging */ }
            }

            // Phase 2: nearest-edge inset (5 mm per step, up to 10 steps)
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
            const double p2StepMm = 5.0;

            for (int step = 0; step < p2Steps; step++)
            {
                double extraInset = step * p2StepMm / 304.8;
                XYZ dir = GetNearestEdgeInwardNormal(pt, boundary);
                XYZ candidate = new XYZ(
                    insetOrigin.X + dir.X * extraInset,
                    insetOrigin.Y + dir.Y * extraInset,
                    slabZ);
                try
                {
                    var before = ed.SlabShapeVertices.Cast<SlabShapeVertex>()
                                   .Select(v => v.Position).ToList();
                    ed.AddPoint(candidate);
                    ZeroNewVertexOffset(ed, before, roof);
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

        // ── Zero offset on newly added vertex ─────────────────────────────────────
        //
        // SlabShapeVertex does not expose an Offset property in Revit 2026.
        // Locates the new vertex by XY diff and logs its position.

        private static void ZeroNewVertexOffset(
            SlabShapeEditor ed,
            List<XYZ> positionsBefore,
            RoofBase roof)
        {
            try
            {
                var edFresh = roof.GetSlabShapeEditor();
                if (edFresh == null) return;

                var after = edFresh.SlabShapeVertices.Cast<SlabShapeVertex>().ToList();

                const double tol = 1.0 / 304.8;
                var newVerts = after.Where(v =>
                    !positionsBefore.Any(p =>
                        Math.Abs(p.X - v.Position.X) < tol &&
                        Math.Abs(p.Y - v.Position.Y) < tol))
                    .ToList();

                foreach (var v in newVerts)
                    System.Diagnostics.Debug.WriteLine(
                        $"[RidgeCreationService] New vertex at " +
                        $"X={v.Position.X * 304.8:F1} Y={v.Position.Y * 304.8:F1} " +
                        $"Z={v.Position.Z * 304.8:F1} mm (Offset API not available in 2026)");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[RidgeCreationService] ZeroNewVertexOffset warning: {ex.Message}");
            }
        }

        // ── TX-02: Detail lines ───────────────────────────────────────────────────

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

        // ── Geometry helpers ──────────────────────────────────────────────────────

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
                    double ex = b.X - a.X, ey = b.Y - a.Y;
                    double len = Math.Sqrt(ex * ex + ey * ey);
                    if (len < 1e-9) continue;
                    double nx = -ey / len, ny = ex / len;
                    XYZ test = new XYZ(mid.X + nx * insetFt, mid.Y + ny * insetFt, 0);
                    if (!RoofGeometry2D.PointInPolygon(test, boundary)) { nx = -nx; ny = -ny; }
                    bestPoint = new XYZ(mid.X + nx * insetFt, mid.Y + ny * insetFt, 0);
                }
            }
            return bestPoint;
        }

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

        private static double GetSlabBaseZ(SlabShapeEditor ed)
        {
            var vertices = ed.SlabShapeVertices.Cast<SlabShapeVertex>().ToList();
            if (vertices.Count == 0) return 0.0;

            const double zTol = 1.0 / 304.8;
            var zs = vertices.Select(v => v.Position.Z).OrderBy(z => z).ToList();

            double bestZ = zs[0];
            int bestCount = 0;
            int i = 0;
            while (i < zs.Count)
            {
                int j = i;
                while (j < zs.Count && zs[j] - zs[i] <= zTol) j++;
                int count = j - i;
                if (count > bestCount) { bestCount = count; bestZ = zs[i + (j - i) / 2]; }
                i = j;
            }
            return bestZ;
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