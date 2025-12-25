using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin.Creaser_V101.Helpers;
using Revit26_Plugin.Creaser_V101.Models;

namespace Revit26_Plugin.Creaser_V100.Services
{
    public class FamilyPlacementService
    {
        private readonly Document _doc;
        private readonly View _view;
        private readonly ILogService _log;

        public FamilyPlacementService(
            Document document,
            View view,
            ILogService log)
        {
            _doc = document;
            _view = view;
            _log = log;
        }

        public void Place(
            ElementId symbolId,
            IList<ProcessedLine> lines)
        {
            using (_log.Scope(nameof(FamilyPlacementService), "Place"))
            {
                if (_view.SketchPlane == null)
                {
                    _log.Error(nameof(FamilyPlacementService),
                        "Active view has no SketchPlane.");
                    return;
                }

                if (lines == null || lines.Count == 0)
                {
                    _log.Warning(nameof(FamilyPlacementService),
                        "No lines to place.");
                    return;
                }

                FamilySymbol symbol =
                    _doc.GetElement(symbolId) as FamilySymbol;

                if (symbol == null)
                {
                    _log.Error(nameof(FamilyPlacementService),
                        "FamilySymbol not found.");
                    return;
                }

                Plane plane = _view.SketchPlane.GetPlane();
                double minLen = _doc.Application.ShortCurveTolerance;

                HashSet<string> placed = new HashSet<string>();

                using (Transaction tx =
                    new Transaction(_doc, "Place Creaser Detail Items"))
                {
                    tx.Start();

                    // -------------------------------------------------
                    // REQUIRED: activate symbol
                    // -------------------------------------------------
                    if (!symbol.IsActive)
                    {
                        symbol.Activate();
                        _doc.Regenerate();
                        _log.Info(nameof(FamilyPlacementService),
                            $"Activated symbol: {symbol.Name}");
                    }

                    foreach (ProcessedLine line in lines)
                    {
                        // -------------------------------------------------
                        // Project endpoints to SketchPlane
                        // -------------------------------------------------
                        XYZ p1 = ProjectionHelper.ProjectToPlane(
                            line.P1_3D, plane);

                        XYZ p2 = ProjectionHelper.ProjectToPlane(
                            line.P2_3D, plane);

                        if (p1.DistanceTo(p2) < minLen)
                            continue;

                        string key =
                            $"{p1.X:F6}_{p1.Y:F6}|{p2.X:F6}_{p2.Y:F6}";

                        if (!placed.Add(key))
                            continue;

                        // -------------------------------------------------
                        // OPTION 1 (CRITICAL):
                        // Create DetailCurve FIRST
                        // -------------------------------------------------
                        DetailCurve detailCurve =
                            _doc.Create.NewDetailCurve(
                                _view,
                                Line.CreateBound(p1, p2));

                        // -------------------------------------------------
                        // Place family USING the DetailCurve geometry
                        // -------------------------------------------------
                        Line lineCurve = detailCurve.GeometryCurve as Line;
                        if (lineCurve != null)
                        {
                            _doc.Create.NewFamilyInstance(
                                lineCurve,
                                symbol,
                                _view);

                            _log.Info(nameof(FamilyPlacementService),
                                "Placed detail family instance.");
                        }
                    }

                    tx.Commit();
                }
            }
        }
    }
}
