using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V00.Services.DetailItems
{
    public class DetailItemCollectorService
    {
        private readonly Document _doc;

        public DetailItemCollectorService(Document doc)
        {
            _doc = doc ?? throw new ArgumentNullException(nameof(doc));
        }

        public IList<FamilySymbol> Collect()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .Cast<FamilySymbol>()
                .Where(IsValidDetailItem)
                .OrderBy(s => s.FamilyName)
                .ThenBy(s => s.Name)
                .ToList();
        }

        private static bool IsValidDetailItem(FamilySymbol symbol)
        {
            if (symbol?.Family == null)
                return false;

            Parameter lengthParam =
                symbol.get_Parameter(
                    BuiltInParameter.FAMILY_LINE_LENGTH_PARAM);

            return lengthParam != null;
        }
    }
}
