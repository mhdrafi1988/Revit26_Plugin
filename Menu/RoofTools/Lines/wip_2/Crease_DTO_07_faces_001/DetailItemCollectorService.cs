using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class DetailItemCollectorService
    {
        private readonly Document _doc;
        private readonly LoggingService _log;

        public DetailItemCollectorService(
            Document doc,
            LoggingService log)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        public IList<FamilySymbol> Collect()
        {
            _log.Info("Collecting detail item symbols.");

            var symbols =
                new FilteredElementCollector(_doc)
                    .OfClass(typeof(FamilySymbol))
                    .OfCategory(BuiltInCategory.OST_DetailComponents)
                    .Cast<FamilySymbol>()
                    .Where(IsValidDetailItem)
                    .OrderBy(s => s.FamilyName)
                    .ThenBy(s => s.Name)
                    .ToList();

            _log.Info($"Detail item symbols collected: {symbols.Count}");
            return symbols;
        }

        private static bool IsValidDetailItem(FamilySymbol symbol)
        {
            if (symbol?.Family == null)
                return false;

            if (!symbol.Family.IsParametric)
                return false;

            Parameter lengthParam =
                symbol.get_Parameter(
                    BuiltInParameter.FAMILY_LINE_LENGTH_PARAM);

            return lengthParam != null;
        }
    }
}
