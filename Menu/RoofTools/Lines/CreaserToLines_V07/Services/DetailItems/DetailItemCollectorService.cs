using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Revit26_Plugin.CreaserAdv_V00_701.Services.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V00_701.Services.DetailItems
{
    /// <summary>
    /// Collects plan-safe detail lines from the active view.
    /// Pure collector – NO creation or modification logic.
    /// </summary>
    public class DetailItemCollectorService
    {
        private readonly Document _doc;
        private readonly ViewPlan _view;

        public DetailItemCollectorService(Document doc, ViewPlan view)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _view = view ?? throw new ArgumentNullException(nameof(view));
        }

        public IList<Line> CollectDetailLines(LoggingService log)
        {
            var results = new List<Line>();
            double tol = _doc.Application.ShortCurveTolerance;

            FilteredElementCollector collector =
                new FilteredElementCollector(_doc, _view.Id)
                    .OfClass(typeof(CurveElement));

            foreach (CurveElement curveElem in collector)
            {
                if (curveElem?.GeometryCurve is not Line line)
                    continue;

                if (line.Length < tol)
                    continue;

                results.Add(line);
            }

            log.Info($"Detail lines collected: {results.Count}");
            return results;
        }
    }
}
