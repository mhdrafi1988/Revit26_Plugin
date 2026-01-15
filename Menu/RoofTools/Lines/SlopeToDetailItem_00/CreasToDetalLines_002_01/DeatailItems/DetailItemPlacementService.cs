using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V002.Services
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

        public void PlaceAlongLines(
            IList<Line> lines,
            FamilySymbol symbol,
            LoggingService log)
        {
            if (!symbol.IsActive)
            {
                symbol.Activate();
                _doc.Regenerate();
            }

            int placed = 0;

            foreach (var line in lines)
            {
                if (line == null)
                    continue;

                var instance =
                    _doc.Create.NewFamilyInstance(
                        line,
                        symbol,
                        _view);

                if (instance != null)
                    placed++;
            }

            log.Info($"Detail items placed: {placed}");
        }
    }
}
