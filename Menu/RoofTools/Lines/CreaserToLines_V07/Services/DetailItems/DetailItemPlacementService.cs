using Autodesk.Revit.DB;
using Revit26_Plugin.CreaserAdv_V00_701.Services.Logging;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V00_701.Services.DetailItems
{
    /// <summary>
    /// Places line-based detail items along lines guaranteed
    /// to lie in the active plan view's sketch plane.
    /// </summary>
    public class DetailItemPlacementService
    {
        private readonly Document _doc;
        private readonly ViewPlan _view;

        public DetailItemPlacementService(Document doc, ViewPlan view)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public void PlaceAlongLines(
            IList<Line> sourceLines,
            FamilySymbol symbol,
            LoggingService log)
        {
            if (sourceLines == null || sourceLines.Count == 0)
            {
                log.Warning("No lines provided for detail item placement.");
                return;
            }

            if (symbol == null)
            {
                log.Warning("Detail item symbol is null.");
                return;
            }

            if (!symbol.IsActive)
            {
                symbol.Activate();
                _doc.Regenerate();
            }

            // Get the exact Z elevation of the plan view
            double viewZ = _view.GenLevel.Elevation;
            double tol = _doc.Application.ShortCurveTolerance;

            int placed = 0;

            foreach (Line src in sourceLines)
            {
                if (src == null || src.Length < tol)
                    continue;

                try
                {
                    // Rebuild endpoints ON the view plane
                    XYZ p0 = src.GetEndPoint(0);
                    XYZ p1 = src.GetEndPoint(1);

                    XYZ p0Plan = new XYZ(p0.X, p0.Y, viewZ);
                    XYZ p1Plan = new XYZ(p1.X, p1.Y, viewZ);

                    Line planLine = Line.CreateBound(p0Plan, p1Plan);

                    _doc.Create.NewFamilyInstance(
                        planLine,
                        symbol,
                        _view);

                    placed++;
                }
                catch (Exception ex)
                {
                    log.Warning($"Failed to place detail item: {ex.Message}");
                }
            }

            log.Info($"Detail items placed: {placed}");
        }
    }
}
