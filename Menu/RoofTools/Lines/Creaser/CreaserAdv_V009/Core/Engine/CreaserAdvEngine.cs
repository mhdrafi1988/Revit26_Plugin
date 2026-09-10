// =======================================================
// File: CreaserAdvEngine.cs
// Location: Core/Engine/
// Pipeline extracted verbatim from CreaserAdvViewModel.Run() (V009) so it
// can run inside CreaserAdvHandler (IExternalEventHandler) instead of
// directly from a button click. A modeless window cannot call the Revit
// API directly from a button click — the roof, detail symbol, and active
// view are all re-resolved here from Ids, only ever running inside a
// valid API context (Command or ExternalEvent handler), matching the
// convention already used by InnerLoopDivider/InnerLoopsAndPerpendicular/
// OuterCurveDivider/AutoSlopeByDrain.
// =======================================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.CreaserAdv.V009.Core.Models;
using Revit26_Plugin.CreaserAdv.V009.Services;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv.V009.Core.Engine
{
    public static class CreaserAdvEngine
    {
        public static CreaserAdvResult Execute(UIApplication app, CreaserAdvPayload payload)
        {
            Document doc = app.ActiveUIDocument.Document;
            LoggingService log = payload.Log;

            Element roof = doc.GetElement(payload.RoofId);
            if (roof == null)
                return new CreaserAdvResult { Success = false, ErrorMessage = "The roof no longer exists in the document." };

            FamilySymbol symbol = doc.GetElement(payload.SelectedDetailSymbolId) as FamilySymbol;
            if (symbol == null)
                return new CreaserAdvResult { Success = false, ErrorMessage = "The selected detail item is no longer valid." };

            if (doc.ActiveView is not ViewPlan planView || planView.GenLevel == null)
                return new CreaserAdvResult { Success = false, ErrorMessage = "Run this command from a Plan View." };

            log.Info("─── Run started ───");

            using var tx = new Transaction(doc, "Creaser Advanced V008 – Place Detail Items");
            tx.Start();

            // ── 1. Extract crease curves ──────────────────────────────────────
            var creaseService = new RoofSharedTopFaceCreaseService(log);
            IList<Curve> creaseCurves = creaseService.ExtractSharedTopFaceCreases(roof);

            if (creaseCurves.Count == 0)
            {
                log.Warning("No crease edges found — transaction rolled back.");
                tx.RollBack();
                return Summary(0, 0, 0, 0);
            }

            // ── 1b. Filter out horizontal creases (same Z on both endpoints) ──
            var horizontalFilterSvc = new HorizontalCreaseFilterService(log);
            creaseCurves = horizontalFilterSvc.FilterOutHorizontalCreases(creaseCurves);

            if (creaseCurves.Count == 0)
            {
                log.Warning("No non-horizontal crease edges found — transaction rolled back.");
                tx.RollBack();
                return Summary(0, 0, 0, 0);
            }

            // ── 2. Extract boundary curves (optional) ─────────────────────────
            IList<Curve> boundaryCurves = new List<Curve>();
            if (payload.IncludeBoundaryLines)
            {
                boundaryCurves = creaseService.ExtractBoundaryLines(roof);
                log.Info($"Boundary lines extracted: {boundaryCurves.Count}");
            }

            // ── 2b. Filter by Dijkstra path validity (optional, ticked by default) ──
            int ridgePointCount = 0;
            int disconnectedPointCount = 0;

            if (payload.EnableDijkstraPathFilter)
            {
                var dijkstraSvc = new DijkstraPathValidityService(log);
                var dijkstraResult = dijkstraSvc.FilterByPathValidity(creaseCurves, boundaryCurves, payload.MinimumSlopePercent, payload.EnableMinimumSlope);
                creaseCurves = dijkstraResult.Creases;
                boundaryCurves = dijkstraResult.Boundary;
                ridgePointCount = dijkstraResult.RidgePoints;
                disconnectedPointCount = dijkstraResult.DisconnectedPoints;

                if (creaseCurves.Count == 0 && boundaryCurves.Count == 0)
                {
                    log.Warning("All curves failed the Dijkstra path validity check — transaction rolled back.");
                    tx.RollBack();
                    return Summary(0, 0, 0, 0, ridgePointCount, disconnectedPointCount);
                }
            }

            // ── 3. Project all curves to plan view ────────────────────────────
            (creaseCurves, IList<Line> creaseLines2d) = ProjectToPlanView(doc, log, creaseCurves, planView, "crease");

            IList<Line> boundaryLines2d;
            if (payload.IncludeBoundaryLines)
                (boundaryCurves, boundaryLines2d) = ProjectToPlanView(doc, log, boundaryCurves, planView, "boundary");
            else
                boundaryLines2d = new List<Line>();

            if (creaseLines2d.Count == 0 && boundaryLines2d.Count == 0)
            {
                log.Warning("All lines collapsed during projection — transaction rolled back.");
                tx.RollBack();
                return Summary(creaseCurves.Count, boundaryCurves.Count, 0, 0);
            }

            // ── 4. Filter creases by minimum length (optional, crease only) ────
            if (payload.EnableMinimumLength)
            {
                var minLengthSvc = new MinimumLengthFilterService(log);
                (creaseCurves, creaseLines2d) =
                    minLengthSvc.FilterByMinimumLength(creaseCurves, creaseLines2d, payload.MinimumLengthMm);

                if (creaseLines2d.Count == 0 && boundaryLines2d.Count == 0)
                {
                    log.Warning("All crease lines filtered out by minimum length — transaction rolled back.");
                    tx.RollBack();
                    return Summary(creaseCurves.Count, boundaryCurves.Count, 0, 0);
                }
            }

            // ── 5. Group creases by drain proximity (optional) ──────────────────
            if (payload.EnableDrainGrouping && creaseLines2d.Count > 0)
            {
                var drainSvc = new DrainPointGroupingService(log);
                double radiusFt = payload.DrainGroupingRadiusMm / 304.8;

                creaseLines2d = drainSvc.FilterByDrainProximity(creaseCurves, creaseLines2d, radiusFt);

                if (creaseLines2d.Count == 0 && boundaryLines2d.Count == 0)
                {
                    log.Warning("All crease lines filtered out by drain grouping — transaction rolled back.");
                    tx.RollBack();
                    return Summary(creaseCurves.Count, boundaryCurves.Count, 0, 0);
                }
            }

            // ── 6. Place detail items ─────────────────────────────────────────
            var allLines = creaseLines2d.Concat(boundaryLines2d).ToList();
            var (placed, failed) = new DetailItemPlacementService(doc, planView)
                .PlaceAlongLines(allLines, symbol, log);

            tx.Commit();

            // ── 7. Summary ────────────────────────────────────────────────────
            log.Info($"─── Run complete — created: {placed}  failed: {failed} ───");
            return Summary(creaseCurves.Count, boundaryCurves.Count, placed, failed, ridgePointCount, disconnectedPointCount);
        }

        private static (IList<Curve> Curves3d, IList<Line> Lines2d) ProjectToPlanView(
            Document doc,
            LoggingService log,
            IList<Curve> curves,
            ViewPlan view,
            string label)
        {
            double viewZ = view.GenLevel.Elevation;
            double tol = doc.Application.ShortCurveTolerance;
            var keptCurves = new List<Curve>();
            var keptLines = new List<Line>();
            int skipped = 0;

            foreach (Curve curve in curves)
            {
                XYZ a = curve.GetEndPoint(0);
                XYZ b = curve.GetEndPoint(1);

                XYZ p1 = new XYZ(a.X, a.Y, viewZ);
                XYZ p2 = new XYZ(b.X, b.Y, viewZ);

                if (p1.DistanceTo(p2) < tol) { skipped++; continue; }

                keptCurves.Add(curve);
                keptLines.Add(Line.CreateBound(p1, p2));
            }

            if (skipped > 0)
                log.Warning($"{skipped} {label} curve(s) projected to zero length — skipped.");

            log.Info($"{char.ToUpper(label[0])}{label.Substring(1)} lines projected: {keptLines.Count}");
            return (keptCurves, keptLines);
        }

        private static CreaserAdvResult Summary(int creasesFound, int boundaryFound, int created, int failed, int ridgePoints = 0, int disconnectedPoints = 0)
            => new CreaserAdvResult
            {
                Success = true,
                CreasesFound = creasesFound,
                BoundaryFound = boundaryFound,
                Created = created,
                Failed = failed,
                RidgePoints = ridgePoints,
                DisconnectedPoints = disconnectedPoints
            };
    }
}
