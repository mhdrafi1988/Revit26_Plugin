using Autodesk.Revit.DB;
using Revit26_Plugin.CreaserAdv_V00.Services.Logging;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V00.Services.DetailItems
{
    /// <summary>
    /// Places detail items using plan-safe lines.
    /// NO geometry logic allowed here.
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
            IList<Line> planLines,
            FamilySymbol symbol,
            LoggingService log)
        {
            if (planLines == null || planLines.Count == 0)
                return;

            if (symbol == null)
                return;

            if (!symbol.IsActive)
            {
                symbol.Activate();
                _doc.Regenerate();
            }

            double tol = _doc.Application.ShortCurveTolerance;
            int placed = 0;

            foreach (Line line in planLines)
            {
                if (line == null || line.Length < tol)
                    continue;

                _doc.Create.NewFamilyInstance(
                    line,
                    symbol,
                    _view);

                placed++;
            }

            log.Info($"Crease detail items placed: {placed}");
        }
    }
}
