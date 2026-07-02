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

namespace Revit26_Plugin.RoofDetailLineIntersect.V008
{
    public partial class RoofDetailLineIntersectViewModel : ObservableObject
    {
        // ── Revit context ────────────────────────────────────────────────────
        private readonly Document _doc;
        private readonly FootPrintRoof _roof;
        private readonly List<DetailLine> _detailLines;
        private readonly UIDocument _uiDoc;
        private readonly List<Line> _allSegments;   // cached boundary segments

        // ── ExternalEvent stored as field to prevent GC ──────────────────────
        private ExternalEvent _exEvent;

        // ── Constants ────────────────────────────────────────────────────────
        private const double DedupToleranceFt = 2.0 / 1000.0 / 0.3048;
        private const double ZeroLengthTolerance = 1e-6;
        private const double RayCastDistance = 100_000.0;
        private const double MinThicknessMarginMm = 3.0;   // minimum thickness left in tapered layer

        // Increased nudge offsets (mm) – now up to 150 mm
        private static readonly double[] NudgeOffsetsMm = { 5, 10, 20, 40, 80, 150 };

        // ── Observable properties ────────────────────────────────────────────
        [ObservableProperty] private string roofName = string.Empty;
        [ObservableProperty] private string roofIdText = string.Empty;
        [ObservableProperty] private string viewName = string.Empty;
        [ObservableProperty] private int totalLines;
        [ObservableProperty] private int intersectionsFound;
        [ObservableProperty] private int pointsPlaced;
        [ObservableProperty] private int skippedCount;
        [ObservableProperty] private bool isBusy;

        public ObservableCollection<LogEntry> LogEntries { get; } = new();

        // ── Commands ─────────────────────────────────────────────────────────
        public ICommand RunCommand { get; }
        public ICommand CopyLogCommand { get; }

        // ── Constructor ──────────────────────────────────────────────────────
        public RoofDetailLineIntersectViewModel(
            UIDocument uiDoc,
            Document doc,
            FootPrintRoof roof,
            List<DetailLine> detailLines)
        {
            _uiDoc = uiDoc;
            _doc = doc;
            _roof = roof;
            _detailLines = detailLines;

            // Pre‑extract boundary segments (outer+inner) once for later use
            var outerSegs = new List<Line>();
            var innerSegs = new List<Line>();
            ExtractBoundarySegments2D(outerSegs, innerSegs, out _, out _, out _);
            _allSegments = outerSegs.Concat(innerSegs).ToList();

            RoofName = roof.Name ?? "Roof";
            RoofIdText = $"id {roof.Id.Value}";
            ViewName = doc.ActiveView?.Name ?? string.Empty;
            TotalLines = detailLines.Count;

            RunCommand = new RelayCommand(ExecuteRun, () => !IsBusy);
            CopyLogCommand = new RelayCommand(ExecuteCopyLog);

            var handler = new PlacePointsEventHandler(_doc, _roof, _detailLines, this);
            _exEvent = ExternalEvent.Create(handler);
        }

        partial void OnIsBusyChanged(bool value)
            => (RunCommand as RelayCommand)?.NotifyCanExecuteChanged();

        private void ExecuteRun()
        {
            IsBusy = true;
            LogEntries.Clear();
            IntersectionsFound = 0;
            PointsPlaced = 0;
            SkippedCount = 0;

            try
            {
                _exEvent.Raise();
            }
            catch (Exception ex)
            {
                AddLog(LogLevel.Error, $"Fatal error raising event: {ex.Message}");
                IsBusy = false;
            }
        }

        // ── IExternalEventHandler (inner class) ──────────────────────────────
        private sealed class PlacePointsEventHandler : IExternalEventHandler
        {
            private readonly Document _doc;
            private readonly FootPrintRoof _roof;
            private readonly List<DetailLine> _detailLines;
            private readonly RoofDetailLineIntersectViewModel _vm;

            public PlacePointsEventHandler(
                Document doc,
                FootPrintRoof roof,
                List<DetailLine> detailLines,
                RoofDetailLineIntersectViewModel vm)
            {
                _doc = doc;
                _roof = roof;
                _detailLines = detailLines;
                _vm = vm;
            }

            public string GetName() => "RoofDetailLineIntersect V008 — Place Shape Points";

            public void Execute(UIApplication app)
            {
                try { _vm.ExecutePlacePoints(); }
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
            // 1. Extract boundary (already done in ctor, but we need separate lists for point‑in‑roof)
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

            // All boundary segments combined (for intersection scanning)
            var allSegments = outerSegments.Concat(innerSegments).ToList();

            // Approximate interior direction reference for inward-nudge retries
            XYZ centroid = ComputeCentroid(outerSegments);

            // 2. Base Z — computed before opening the transaction (read-only)
            double baseZ = GetBaseZ();
            AddLog(LogLevel.Info, $"Base Z = {baseZ * 0.3048:F3} m");

            // 2b. Thickness safety — abort before touching the model if this
            // roof type's tapered layer has no safe margin at all.
            if (!ValidateThicknessSafety(baseZ, out double minSafeZ))
                return;

            // ── Open outer transaction ─────────────────────────────────────
            using (var tx = new Transaction(_doc, "RoofDetailLineIntersect V008 — Place Shape Points"))
            {
                tx.Start();

                // Enable shape editing once, up front.
                SlabShapeEditor sseInit = _roof.GetSlabShapeEditor();
                if (!sseInit.IsEnabled)
                    sseInit.Enable();

                var placedXYs = new List<XYZ>(); // global dedup list
                int lineIndex = 0;

                foreach (var dl in _detailLines)
                {
                    lineIndex++;
                    try
                    {
                        ProcessDetailLine(lineIndex, dl, outerSegments, innerSegments,
                                          allSegments, baseZ, placedXYs, centroid, minSafeZ);
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
        }

        // ── Process single DetailLine ────────────────────────────────────────
        private void ProcessDetailLine(
            int lineIndex,
            DetailLine dl,
            List<Line> outerSegments,
            List<Line> innerSegments,
            List<Line> allSegments,
            double baseZ,
            List<XYZ> placedXYs,
            XYZ centroid,
            double minSafeZ)
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

            var candidateHits = new List<XYZ>();

            foreach (var dlSeg in dlSegments)
            {
                // A) Bounded intersection: where this segment crosses any boundary edge
                var hits = FindIntersectionsBounded(dlSeg, allSegments);
                candidateHits.AddRange(hits);

                // B) Endpoints that lie strictly inside the roof (outer - inner holes)
                var p0 = Flatten(dlSeg.GetEndPoint(0));
                var p1 = Flatten(dlSeg.GetEndPoint(1));

                if (IsPointInsideRoof(p0, outerSegments, innerSegments))
                    candidateHits.Add(p0);
                if (IsPointInsideRoof(p1, outerSegments, innerSegments))
                    candidateHits.Add(p1);
            }

            var uniqueHits = DeduplicatePoints(candidateHits);

            if (uniqueHits.Count == 0)
            {
                AddLog(LogLevel.Warning,
                    $"Line {lineIndex} (id {dl.Id.Value}) — no intersections or interior points.");
                SkippedCount++;
                return;
            }

            foreach (var hit in uniqueHits)
            {
                IntersectionsFound++;

                if (IsDuplicate(hit, placedXYs))
                {
                    AddLog(LogLevel.Warning,
                        $"Line {lineIndex} → ({ToM(hit.X):F3}, {ToM(hit.Y):F3}) m — global dedup, skipped.");
                    SkippedCount++;
                    continue;
                }

                var pt3D = new XYZ(hit.X, hit.Y, baseZ);

                // Use SubTransaction for each point attempt to isolate failures
                using (var subTx = new SubTransaction(_doc))
                {
                    subTx.Start();

                    if (TryAddPointWithNudge(_roof, _doc, pt3D, baseZ, outerSegments, allSegments, centroid, minSafeZ,
                            out double nudgeMm, out XYZ actualXY, out string failReason))
                    {
                        placedXYs.Add(actualXY);
                        PointsPlaced++;

                        string suffix = nudgeMm > 0 ? $" (nudged {nudgeMm:F0} mm inward)" : "";
                        AddLog(LogLevel.Success,
                            $"Line {lineIndex} → ({ToM(hit.X):F3}, {ToM(hit.Y):F3}) m — placed{suffix}.");
                        subTx.Commit();   // commit this single point
                    }
                    else
                    {
                        SkippedCount++;
                        AddLog(LogLevel.Warning,
                            $"Line {lineIndex} → ({ToM(hit.X):F3}, {ToM(hit.Y):F3}) m — skipped: {failReason}");
                        subTx.RollBack(); // discard any partial changes
                    }
                }
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
        private static List<XYZ> FindIntersectionsBounded(Line querySegment, List<Line> boundarySegs)
        {
            var results = new List<XYZ>();

            XYZ p = querySegment.GetEndPoint(0);
            XYZ r = querySegment.GetEndPoint(1) - p;

            foreach (var seg in boundarySegs)
            {
                XYZ q = seg.GetEndPoint(0);
                XYZ s = seg.GetEndPoint(1) - q;

                double rxs = Cross2D(r, s);
                XYZ qmp = q - p;
                double qpxr = Cross2D(qmp, r);
                double qpxs = Cross2D(qmp, s);

                if (Math.Abs(rxs) < 1e-10) continue;

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

        // ── Point-in-roof test ──────────────────────────────────────────────
        private static bool IsPointInsideRoof(
            XYZ point,
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
            int crossings = 0;
            XYZ rayEnd = new XYZ(point.X + RayCastDistance, point.Y, 0);

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
            out int outerEdgeCount,
            out int innerLoopCount,
            out int innerEdgeCount)
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
                        try { pts = c.Tessellate(); }
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
                else { innerLoopCount++; innerEdgeCount += edgesAdded; }
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
            Level lvl = _doc.GetElement(_roof.LevelId) as Level;
            double levelZ = lvl?.Elevation ?? 0.0;

            double offsetFt = 0.0;
            Parameter offsetParam = _roof.get_Parameter(BuiltInParameter.ROOF_LEVEL_OFFSET_PARAM);
            if (offsetParam != null && offsetParam.StorageType == StorageType.Double)
                offsetFt = offsetParam.AsDouble();

            return levelZ + offsetFt;
        }

        // ── Thickness safety ─────────────────────────────────────────────────
        private bool ValidateThicknessSafety(double baseZ, out double minSafeZ)
        {
            minSafeZ = double.NegativeInfinity;

            RoofType roofType = _doc.GetElement(_roof.GetTypeId()) as RoofType;
            CompoundStructure cs = roofType?.GetCompoundStructure();

            if (cs == null)
            {
                AddLog(LogLevel.Info, "Roof type has no compound structure data — skipping thickness safety check.");
                return true;
            }

            double totalMm = cs.GetWidth() * 304.8;
            int variableIndex = cs.VariableLayerIndex;

            if (variableIndex < 0)
            {
                AddLog(LogLevel.Info,
                    $"Roof compound structure: {totalMm:F0} mm total, no tapered layer — " +
                    "shape-edit Z is not layer-thickness constrained.");
                return true;
            }

            double variableWidthMm = cs.GetLayerWidth(variableIndex) * 304.8;
            AddLog(LogLevel.Info,
                $"Roof compound structure: {totalMm:F0} mm total, tapered layer = {variableWidthMm:F0} mm.");

            if (variableWidthMm <= MinThicknessMarginMm)
            {
                AddLog(LogLevel.Error,
                    $"Tapered layer ({variableWidthMm:F0} mm) leaves no safe margin for shape editing. Aborting.");
                return false;
            }

            minSafeZ = baseZ - ((variableWidthMm - MinThicknessMarginMm) / 304.8);
            AddLog(LogLevel.Info,
                $"Safe Z floor: {ToM(minSafeZ):F3} m (tapered-layer headroom, {MinThicknessMarginMm:F0} mm margin reserved).");
            return true;
        }

        // ── Helpers ├─────────────────────────────────────────────────────────
        private static XYZ Flatten(XYZ p) => new XYZ(p.X, p.Y, 0);
        private static double Cross2D(XYZ a, XYZ b) => a.X * b.Y - a.Y * b.X;
        private static double ToM(double feet) => feet * 0.3048;

        private static XYZ ComputeCentroid(List<Line> outerSegments)
        {
            double sx = 0, sy = 0;
            int n = 0;
            foreach (var seg in outerSegments)
            {
                var p = seg.GetEndPoint(0);
                sx += p.X; sy += p.Y; n++;
            }
            return n > 0 ? new XYZ(sx / n, sy / n, 0) : XYZ.Zero;
        }

        // ── NEW: Get inward direction using nearest boundary normal ──────────
        private XYZ GetInwardDirection(XYZ pt, List<Line> boundarySegs, XYZ centroid)
        {
            // Find the closest boundary segment
            double minDist = double.MaxValue;
            Line closestSeg = null;

            foreach (var seg in boundarySegs)
            {
                var p0 = seg.GetEndPoint(0);
                var p1 = seg.GetEndPoint(1);
                // Project point onto segment
                double t = ((pt.X - p0.X) * (p1.X - p0.X) + (pt.Y - p0.Y) * (p1.Y - p0.Y)) /
                           ((p1 - p0).X * (p1 - p0).X + (p1 - p0).Y * (p1 - p0).Y);
                t = Math.Max(0, Math.Min(1, t));
                XYZ proj = p0 + t * (p1 - p0);
                double dist = pt.DistanceTo(proj);
                if (dist < minDist)
                {
                    minDist = dist;
                    closestSeg = seg;
                }
            }

            if (closestSeg == null)
                return (centroid - pt).Normalize();

            // Compute normal (perpendicular to segment direction)
            XYZ dir = (closestSeg.GetEndPoint(1) - closestSeg.GetEndPoint(0)).Normalize();
            // Two possible normals: (dir.Y, -dir.X) and (-dir.Y, dir.X)
            XYZ normal1 = new XYZ(dir.Y, -dir.X, 0);
            XYZ normal2 = new XYZ(-dir.Y, dir.X, 0);

            // Choose the one pointing toward the centroid
            XYZ toCentroid = (centroid - pt).Normalize();
            if (normal1.DotProduct(toCentroid) > 0)
                return normal1;
            else
                return normal2;
        }

        // ── NEW: Get actual roof top elevation at given XY ──────────────────
        private double GetTopElevationAtXY(XYZ xy)
        {
            // Cast a vertical ray upward from far below to intersect the roof's solid
            double zLow = -1000.0; // far below
            double zHigh = 1000.0; // far above
            XYZ start = new XYZ(xy.X, xy.Y, zLow);
            XYZ end = new XYZ(xy.X, xy.Y, zHigh);
            Line ray = Line.CreateBound(start, end);

            Options opts = new Options { ComputeReferences = true, DetailLevel = ViewDetailLevel.Medium };
            GeometryElement geo = _roof.get_Geometry(opts);
            double topZ = double.NegativeInfinity;

            // Create default SolidCurveIntersectionOptions
            var sciOptions = new SolidCurveIntersectionOptions();

            foreach (GeometryObject obj in geo)
            {
                if (obj is Solid solid && solid.Volume > 0)
                {
                    // Intersect ray with solid
                    var intersections = solid.IntersectWithCurve(ray, sciOptions);
                    for (int i = 0; i < intersections.SegmentCount; i++)
                    {
                        Curve seg = intersections.GetCurveSegment(i);
                        var segStart = seg.GetEndPoint(0);
                        var segEnd = seg.GetEndPoint(1);
                        // Find the highest intersection point
                        if (segStart.Z > topZ) topZ = segStart.Z;
                        if (segEnd.Z > topZ) topZ = segEnd.Z;
                    }
                }
                // Also check instances (could be nested)
                if (obj is GeometryInstance inst)
                {
                    var instGeo = inst.GetInstanceGeometry();
                    foreach (GeometryObject o in instGeo)
                    {
                        if (o is Solid s && s.Volume > 0)
                        {
                            var ints = s.IntersectWithCurve(ray, sciOptions);
                            for (int i = 0; i < ints.SegmentCount; i++)
                            {
                                Curve seg = ints.GetCurveSegment(i);
                                var segStart = seg.GetEndPoint(0);
                                var segEnd = seg.GetEndPoint(1);
                                if (segStart.Z > topZ) topZ = segStart.Z;
                                if (segEnd.Z > topZ) topZ = segEnd.Z;
                            }
                        }
                    }
                }
            }

            return topZ > -999 ? topZ : double.NaN;
        }

        // ── Main point adder with local thickness adjustment ──────────────
        private bool TryAddPointWithNudge(
            FootPrintRoof roof,
            Document doc,
            XYZ pt,
            double baseZ,
            List<Line> outerSegments,
            List<Line> allSegments,
            XYZ centroid,
            double minSafeZ,
            out double appliedNudgeMm,
            out XYZ actualXY,
            out string failureReason)
        {
            appliedNudgeMm = 0;
            actualXY = new XYZ(pt.X, pt.Y, 0);
            failureReason = string.Empty;

            // First, try the original point with local thickness adjustment
            if (TryAddWithLocalThickness(roof, doc, pt.X, pt.Y, baseZ, minSafeZ, out double adjustedZ, out failureReason))
            {
                actualXY = new XYZ(pt.X, pt.Y, 0);
                return true;
            }

            // Nudge inward using the nearest boundary normal
            XYZ dir = GetInwardDirection(pt, allSegments, centroid);
            double len = dir.GetLength();
            if (len < 1e-9)
                return false; // cannot nudge

            dir = dir / len; // normalize

            foreach (double mm in NudgeOffsetsMm)
            {
                double offsetFt = mm / 304.8;
                double nx = pt.X + dir.X * offsetFt;
                double ny = pt.Y + dir.Y * offsetFt;

                // Check that new point is inside roof (strictly)
                if (!IsPointInsideRoof(new XYZ(nx, ny, 0), outerSegments, new List<Line>()))
                    continue; // try next offset

                if (TryAddWithLocalThickness(roof, doc, nx, ny, baseZ, minSafeZ, out adjustedZ, out failureReason))
                {
                    appliedNudgeMm = mm;
                    actualXY = new XYZ(nx, ny, 0);
                    return true;
                }
            }

            failureReason = "Could not place point after all nudge attempts.";
            return false;
        }

        // ── TryAdd with local thickness adjustment ─────────────────────────
        private bool TryAddWithLocalThickness(
            FootPrintRoof roof,
            Document doc,
            double x,
            double y,
            double baseZ,
            double minSafeZ,
            out double adjustedZ,
            out string failureReason)
        {
            adjustedZ = baseZ;
            failureReason = string.Empty;

            // Global safety check
            if (baseZ < minSafeZ)
            {
                failureReason = $"Z {baseZ * 304.8:F0} mm below global safe thickness floor.";
                return false;
            }

            // Get actual top elevation at this XY
            double topZ = GetTopElevationAtXY(new XYZ(x, y, 0));
            if (!double.IsNaN(topZ))
            {
                // Ensure we leave at least MinThicknessMarginMm of the tapered layer
                double minThicknessFt = MinThicknessMarginMm / 304.8;
                double requiredZ = topZ - minThicknessFt;
                if (baseZ < requiredZ)
                {
                    // Raise Z to the required minimum
                    adjustedZ = requiredZ;
                }
                // else keep baseZ
            }
            // If we couldn't sample (e.g., point outside solid), still try baseZ

            // Now attempt to add the point
            SlabShapeEditor sse = roof.GetSlabShapeEditor();
            if (!sse.IsEnabled)
                sse.Enable();

            SlabShapeVertex vertex = null;
            try
            {
                vertex = sse.AddPoint(new XYZ(x, y, adjustedZ));
            }
            catch (Exception ex)
            {
                failureReason = ex.Message;
                return false;
            }

            // Regenerate within the subtransaction – if fails, rollback will happen outside
            try
            {
                doc.Regenerate();
                return true;
            }
            catch (Exception ex)
            {
                failureReason = $"rejected on regenerate: {ex.Message}";
                // Cleanup (though subtransaction rollback will remove the point anyway)
                try
                {
                    SlabShapeEditor cleanup = roof.GetSlabShapeEditor();
                    cleanup.DeletePoint(vertex);
                }
                catch { /* ignore */ }
                return false;
            }
        }

        // ── Dedup ───────────────────────────────────────────────────────────
        private List<XYZ> DeduplicatePoints(List<XYZ> points)
        {
            var unique = new List<XYZ>();
            foreach (var pt in points)
                if (!IsDuplicate(pt, unique)) unique.Add(pt);
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