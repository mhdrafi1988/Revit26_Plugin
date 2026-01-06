using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V003.Services
{
    public class DetailItemPlacementService
    {
        private readonly Document _doc;
        private readonly ViewPlan _view;

        public DetailItemPlacementService(Document doc, ViewPlan view)
        {
            _doc = doc;
            _view = view;
        }

        public void Place(
            IList<Line> lines,
            FamilySymbol symbol,
            LoggingService log)
        {
            if (!symbol.IsActive)
            {
                symbol.Activate();
                _doc.Regenerate();
            }

            int count = 0;
            foreach (var l in lines)
            {
                _doc.Create.NewFamilyInstance(l, symbol, _view);
                count++;
            }

            log.Info($"Placed {count} detail items.");
        }
    }
}
