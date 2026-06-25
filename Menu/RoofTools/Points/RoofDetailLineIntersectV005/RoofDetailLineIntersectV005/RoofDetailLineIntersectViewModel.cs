using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.Shared.Models;   // LogEntry, LogLevel

namespace Revit26_Plugin.RoofDetailLineIntersect.V005
{
    public partial class RoofDetailLineIntersectViewModel : ObservableObject
    {
        // ── Revit context ────────────────────────────────────────────────────
        private readonly Document        _doc;
        private readonly FootPrintRoof   _roof;
        private readonly List<DetailLine> _detailLines;
        private readonly UIDocument      _uiDoc;

        // ── ExternalEvent stored as field to prevent GC ──────────────────────
        private ExternalEvent _exEvent;

        // ── Constants ────────────────────────────────────────────────────────
        /// <summary>2 mm dedup tolerance in Revit internal feet.</summary>
        private const double DedupToleranceFt   = 2.0 / 1000.0 / 0.3048;
        /// <summary>Zero-length tolerance for segment culling.</summary>
        private const double ZeroLengthTolerance = 1e-6;
        /// <summary>Ray-casting distance (must be >> model extents).</summary>
        private const double RayCastDistance     = 100_000.0;

        // ── Observable properties ────────────────────────────────────────────
        [ObservableProperty] private string roofName     = string.Empty;
        [ObservableProperty] private string roofIdText   = string.Empty;
        [ObservableProperty] private string viewName     = string.Empty;
        [ObservableProperty] private int    totalLines;
        [ObservableProperty] private int    intersectionsFound;
        [ObservableProperty] private int    pointsPlaced;
        [ObservableProperty] private int    skippedCount;
        [ObservableProperty] private bool   isBusy;

        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        // ── Commands ─────────────────────────────────────────────────────────
        public ICommand RunCommand     { get; }
        public ICommand CopyLogCommand { get; }

        // ── Constructor ──────────────────────────────────────────────────────
        public RoofDetailLineIntersectViewModel(
            UIDocument        uiDoc,
            Document          doc,
            FootPrintRoof     roof,
            List<DetailLine>  detailLines)
        {
            _uiDoc       = uiDoc;
            _doc         = doc;
            _roof        = roof;
            _detailLines = detailLines;

            RoofName   = roof.Name ?? "Roof";
            RoofIdText = $"id {roof.Id.Value}";
            ViewName   = doc.ActiveView?.Name ?? string.Empty;
            TotalLines = detailLines.Count;

            RunCommand     = new RelayCommand(ExecuteRun, () => !IsBusy);
            CopyLogCommand = new RelayCommand(ExecuteCopyLog);
        }

        partial void OnIsBusyChanged(bool value)
            => (RunCommand as RelayCommand)?.NotifyCanExecuteChanged();

        // ── Trigger ExternalEvent ─────────────────────────────────────────────
        private void ExecuteRun()
        {
            IsBusy = true;
            LogEntries.Clear();
            IntersectionsFound = 0;
            PointsPlaced       = 0;
            SkippedCount       = 0;

            try
            {
                // Store as field — prevents GC before Revit fires the event
                var handler = new PlacePointsEventHandler(_doc, _roof, _detailLines, this);
                _exEvent    = ExternalEvent.Create(handler);
                _exEvent.Raise();
            }
            catch (Exception ex)
            {
                AddLog(LogLevel.Error, $"Fatal error queuing event: {ex.Message}");
                IsBusy = false;
            }
        }

        // ── IExternalEventHandler (inner class) ──────────────────────────────
        private sealed class PlacePointsEventHandler : IExternalEventHandler
        {
            private readonly Document         _doc;
            private readonly FootPrintRoof    _roof;
            private readonly List<DetailLine> _detailLines;
            private readonly RoofDetailLineIntersectViewModel _vm;

            public PlacePointsEventHandler(
                Document        doc,
                FootPrintRoof   roof,
                List<DetailLine> detailLines,
                RoofDetailLineIntersectViewModel vm)
            {
                _doc         = doc;
                _roof        = roof;
                _detailLines = detailLines;
                _vm          = vm;
            }

            public string GetName() => "RoofDetailLineIntersect V005 — Place Shape Points";

            public void Execute(UIApplication app)
            {
                try   { _vm.ExecutePlacePoints(); }
                catch (Exception ex)
                {
                    _vm.AddLog(LogLevel.Error, $"Event handler error: {ex.Message}");
                }
                finally
                {
                    _vm.IsBusy = false;
                }
            }
        }

        // ── Main processing — runs inside ExternalEvent on Revit API thread ──
        private void ExecutePlacePoints()
        {
            // 1. Extract boundary — separate outer and inner loop segments
            var outerSegments = new List<Line>();
            var innerSegments = new List<Line>();
            ExtractBoundarySegments2D(outerSegments, innerSegments,
                out int outerCount, out int innerLoops, out int innerEdgeCount);

            AddLog(LogLevel.Info, $"Roof id {_roof.Id.Value}: {outerCount} outer edges, " +
                                  $"{innerLoops} inner loops ({innerEdgeCount} edges).");

            if (outerSegments.Count == 0)
            {
                AddLog(LogLevel.Error, "No outer boundary segments found. Aborting.");
                return;
            }

            // 2. Base Z — computed before opening the transaction (read-only)
            double baseZ = GetBaseZ();
            AddLog(LogLevel.Info, $"Base Z = {baseZ * 0.3048:F3} m");

            // 3. Open one transaction for all point placements
            using var tx = new Transaction(_doc, "RoofDetailLineIntersect V005 — Place Shape Points");
            try
            {
                tx.Start();

                // Get SlabShapeEditor once, inside the transaction
                SlabShapeEditor sse = _roof.GetSlabShapeEditor();
                if (!sse.IsEnabled)
                    sse.Enable();

                var placedXYs = new List<XYZ>(); // global dedup list
                int lineIndex = 0;

                foreach (var dl in _detailLines)
                {
                    lineIndex++;
                    try
                    {
                        ProcessDetailLine(lineIndex, dl, outerSegments, innerSegments,
                                          baseZ, sse, placedXYs);
                    }
                    catch (Exception ex)
                    {
                        AddLog(LogLevel.Error,
                            $"Line {lineIndex} (id {dl.Id.Value}) — exception: {ex.Message}");
                    }
                }

                tx.Commit();

                int errorCount = LogEntries.Count(e => e.Level == LogLevel.Error);
                AddLog(LogLevel.Info,
                    $"Done — {PointsPlaced} placed, {SkippedCount} skipped, {errorCount} errors.");
            }
            catch (Exception ex)
            {
                if (tx.GetStatus() == TransactionStatus.Started)
                    tx.RollBack();

                AddLog(LogLevel.Error, $"Transaction aborted: {ex.Message}");
            }
        }

        // ── Process single DetailLine ────────────────────────────────────────
        private void ProcessDetailLine(
            int              lineIndex,
            DetailLine       dl,
            List<Line>       outerSegments,
            List<Line>       innerSegments,
            double           baseZ,
            SlabShapeEditor  sse,
            List<XYZ>        placedXYs)
        {
            // Tessellate the detail line into 2D line segments
            var dlSegments = TessellateDetailLine2D(dl);
            if (dlSegments.Count == 0)
            {
                AddLog(LogLevel.Warning,
                    $"Line {lineIndex} (id {dl.Id.Value}) — no geometry or zero length.");
                SkippedCount++;
                return;
            }

            // ── Collect hits, tracking whether each came from outer or inner ──
            // Each entry: (XYZ point, bool isInner)
            var candidateHits = new List<(XYZ pt, bool isInner)>();

            foreach (var dlSeg in dlSegments)
            {
                // A) Intersections with outer boundary edges
                foreach (var hit in FindIntersectionsBounded(dlSeg, outerSegments))
                    candidateHits.Add((hit, false));

                // B) Intersections with inner loop boundary edges — NEW
                if (innerSegments.Count > 0)
                {
                    foreach (var hit in FindIntersectionsBounded(dlSeg, innerSegments))
                        candidateHits.Add((hit, true));
                }

                // C) Endpoints that lie strictly inside the roof surface
                //    (inside outer boundary AND outside all inner holes)
                var p0 = Flatten(dlSeg.GetEndPoint(0));
                var p1 = Flatten(dlSeg.GetEndPoint(1));

                if (IsPointInsideRoof(p0, outerSegments, innerSegments))
                    candidateHits.Add((p0, false));
                if (IsPointInsideRoof(p1, outerSegments, innerSegments))
                    candidateHits.Add((p1, false));
            }

            // Deduplicate preserving isInner flag (first occurrence wins)
            var uniqueHits = DeduplicateTaggedPoints(candidateHits);

            if (uniqueHits.Count == 0)
            {
                AddLog(LogLevel.Warning,
                    $"Line {lineIndex} (id {dl.Id.Value}) — no intersections or interior points.");
                SkippedCount++;
                return;
            }

            foreach (var (hit, isInner) in uniqueHits)
            {
                IntersectionsFound++;

                string tag = isInner ? " [INNER]" : string.Empty;

                if (IsDuplicate(hit, placedXYs))
                {
                    AddLog(LogLevel.Warning,
                        $"Line {lineIndex}{tag} → ({ToM(hit.X):F3}, {ToM(hit.Y):F3}) m — global dedup, skipped.");
                    SkippedCount++;
                    continue;
                }

                var pt3D = new XYZ(hit.X, hit.Y, baseZ);
                sse.AddPoint(pt3D);
                placedXYs.Add(hit);
                PointsPlaced++;

                AddLog(LogLevel.Success,
                    $"Line {lineIndex}{tag} → ({ToM(hit.X):F3}, {ToM(hit.Y):F3}) m — placed.");
            }
        }

        // ── Tessellate DetailLine → 2D line segments ─────────────────────────
        private List<Line> TessellateDetailLine2D(DetailLine dl)
        {
            var result = new List<Line>();
            Curve c = dl.GeometryCurve;
            if (c == null) return result;

            try
            {
                if (c is Line)
                {
                    var s = Flatten(c.GetEndPoint(0));
                    var e = Flatten(c.GetEndPoint(1));
                    if (s.DistanceTo(e) > ZeroLengthTolerance)
                        result.Add(Line.CreateBound(s, e));
                    return result;
                }

                // Arc, circle, spline, ellipse, etc.
                var pts = c.Tessellate();
                if (pts == null || pts.Count < 2)
                {
                    AddLog(LogLevel.Warning,
                        $"DetailLine id {dl.Id.Value} — tessellation: insufficient points.");
                    return result;
                }

                for (int i = 0; i < pts.Count - 1; i++)
                {
                    var p0 = Flatten(pts[i]);
                    var p1 = Flatten(pts[i + 1]);
                    if (p0.DistanceTo(p1) > ZeroLengthTolerance)
                        result.Add(Line.CreateBound(p0, p1));
                }

                if (result.Count > 0)
                    AddLog(LogLevel.Info,
                        $"DetailLine id {dl.Id.Value} — arc/curve → {result.Count} segments.");
            }
            catch (Exception ex)
            {
                AddLog(LogLevel.Warning,
                    $"DetailLine id {dl.Id.Value} — tessellation failed: {ex.Message}");
            }

            return result;
        }

        // ── Bounded intersection: query segment vs boundary segments ─────────
        /// <summary>
        /// Returns points where the query segment [p,p+r] crosses any boundary
        /// segment. Both t (query) and u (boundary) are clamped to [0,1].
        /// </summary>
        private static List<XYZ> FindIntersectionsBounded(Line querySegment, List<Line> boundarySegs)
        {
            var results = new List<XYZ>();

            XYZ p = querySegment.GetEndPoint(0);
            XYZ r = querySegment.GetEndPoint(1) - p;

            foreach (var seg in boundarySegs)
            {
                XYZ q = seg.GetEndPoint(0);
                XYZ s = seg.GetEndPoint(1) - q;

                double rxs  = Cross2D(r, s);
                XYZ   qmp   = q - p;
                double qpxr = Cross2D(qmp, r);
                double qpxs = Cross2D(qmp, s);

                if (Math.Abs(rxs) < 1e-10) continue; // parallel / collinear

                double t = qpxs / rxs;
                double u = qpxr / rxs;

                const double eps = 1e-6;
                if (t >= -eps && t <= 1.0 + eps &&
                    u >= -eps && u <= 1.0 + eps)
                {
                    XYZ hit = p + t * r;
                    results.Add(new XYZ(hit.X, hit.Y, 0));
                }
            }

            return results;
        }

        // ── Point-in-roof test (outer polygon minus inner holes) ─────────────
        /// <summary>
        /// A point is inside the roof surface if it is inside the outer boundary
        /// AND outside all inner holes.
        /// </summary>
        private static bool IsPointInsideRoof(
            XYZ        point,
            List<Line> outerSegments,
            List<Line> innerSegments)
        {
            if (!IsPointInsidePolygon(point, outerSegments)) return false;

            if (innerSegments.Count > 0 &&
                IsPointInsidePolygon(point, innerSegments))
                return false;

            return true;
        }

        private static bool IsPointInsidePolygon(XYZ point, List<Line> segments)
        {
            int  crossings = 0;
            XYZ  rayEnd    = new XYZ(point.X + RayCastDistance, point.Y, 0);

            foreach (var seg in segments)
            {
                if (DoSegmentsIntersect2D(point, rayEnd,
                        seg.GetEndPoint(0), seg.GetEndPoint(1), out _))
                    crossings++;
            }

            return (crossings % 2) == 1;
        }

        private static bool DoSegmentsIntersect2D(
            XYZ p1, XYZ p2, XYZ p3, XYZ p4, out XYZ intersection)
        {
            intersection = XYZ.Zero;
            double x1 = p1.X, y1 = p1.Y, x2 = p2.X, y2 = p2.Y;
            double x3 = p3.X, y3 = p3.Y, x4 = p4.X, y4 = p4.Y;

            double denom = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            if (Math.Abs(denom) < 1e-10) return false;

            double t = ((x1 - x3) * (y3 - y4) - (y1 - y3) * (x3 - x4)) / denom;
            double u = ((x1 - x3) * (y1 - y2) - (y1 - y3) * (x1 - x2)) / denom;

            if (t >= 0 && t <= 1 && u >= 0 && u <= 1)
            {
                intersection = new XYZ(x1 + t * (x2 - x1), y1 + t * (y2 - y1), 0);
                return true;
            }

            return false;
        }

        // ── Boundary extraction ───────────────────────────────────────────────
        private void ExtractBoundarySegments2D(
            List<Line> outerSegments,
            List<Line> innerSegments,
            out int    outerEdgeCount,
            out int    innerLoopCount,
            out int    innerEdgeCount)
        {
            outerEdgeCount = 0;
            innerLoopCount = 0;
            innerEdgeCount = 0;

            ModelCurveArrArray profiles = _roof.GetProfiles();
            bool isFirst = true;

            foreach (ModelCurveArray loop in profiles)
            {
                var targetList = isFirst ? outerSegments : innerSegments;
                int edgesAdded = 0;

                foreach (ModelCurve mc in loop)
                {
                    Curve c = mc.GeometryCurve;
                    if (c == null) continue;

                    if (c is Line)
                    {
                        var s = Flatten(c.GetEndPoint(0));
                        var e = Flatten(c.GetEndPoint(1));
                        if (TryAddSegment(targetList, s, e)) edgesAdded++;
                    }
                    else
                    {
                        IList<XYZ> pts;
                        try   { pts = c.Tessellate(); }
                        catch { continue; }

                        if (pts == null || pts.Count < 2) continue;

                        for (int i = 0; i < pts.Count - 1; i++)
                        {
                            if (TryAddSegment(targetList,
                                    Flatten(pts[i]), Flatten(pts[i + 1])))
                                edgesAdded++;
                        }
                    }
                }

                if (isFirst) { outerEdgeCount = edgesAdded; isFirst = false; }
                else         { innerLoopCount++; innerEdgeCount += edgesAdded; }
            }
        }

        private static bool TryAddSegment(List<Line> list, XYZ s, XYZ e)
        {
            if (s.DistanceTo(e) <= ZeroLengthTolerance) return false;
            list.Add(Line.CreateBound(s, e));
            return true;
        }

        // ── Base Z ────────────────────────────────────────────────────────────
        private double GetBaseZ()
        {
            SlabShapeEditor sse      = _roof.GetSlabShapeEditor();
            var             vertices = sse.SlabShapeVertices;

            if (vertices == null || vertices.Size == 0)
            {
                Level lvl = _doc.GetElement(_roof.LevelId) as Level;
                return lvl?.Elevation ?? 0.0;
            }

            double minZ = double.MaxValue;
            foreach (SlabShapeVertex v in vertices)
                if (v.Position.Z < minZ) minZ = v.Position.Z;

            return minZ;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static XYZ     Flatten(XYZ p)         => new XYZ(p.X, p.Y, 0);
        private static double  Cross2D(XYZ a, XYZ b)  => a.X * b.Y - a.Y * b.X;
        private static double  ToM(double feet)        => feet * 0.3048;

        /// <summary>
        /// Deduplicates a list of tagged hits within the 2mm tolerance.
        /// First occurrence of a location wins (preserving its isInner flag).
        /// </summary>
        private List<(XYZ pt, bool isInner)> DeduplicateTaggedPoints(
            List<(XYZ pt, bool isInner)> points)
        {
            var unique = new List<(XYZ pt, bool isInner)>();
            foreach (var item in points)
            {
                bool isDup = unique.Any(u =>
                    Math.Sqrt(Math.Pow(item.pt.X - u.pt.X, 2) +
                              Math.Pow(item.pt.Y - u.pt.Y, 2)) < DedupToleranceFt);
                if (!isDup)
                    unique.Add(item);
            }
            return unique;
        }

        private static bool IsDuplicate(XYZ candidate, List<XYZ> existing)
            => existing.Any(p =>
                   Math.Sqrt(Math.Pow(candidate.X - p.X, 2) +
                             Math.Pow(candidate.Y - p.Y, 2)) < DedupToleranceFt);

        // ── Log & Copy ────────────────────────────────────────────────────────
        internal void AddLog(LogLevel level, string message)
            => LogEntries.Add(new LogEntry(level, message));

        private void ExecuteCopyLog()
        {
            if (LogEntries.Count == 0) return;
            var sb = new StringBuilder();
            foreach (var entry in LogEntries)
                sb.AppendLine(entry.ToString());
            Clipboard.SetText(sb.ToString());
        }
    }
}
