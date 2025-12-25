using Autodesk.Revit.DB;
//using Revit26_Plugin.AutoLiner_V04.Services;
using Revit26_Plugin.Creaser_V03_03.Helpers;
using Revit26_Plugin.Creaser_V03_03.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Revit26_Plugin.Creaser_V03_03.Services
{
    public static class DetailItemPlacementService
    {
        public static ObservableCollection<FamilySymbol> GetLineBasedFamilies(
            Document doc,
            UiLogService log)
        {
            var list = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(f => f.Family.FamilyPlacementType ==
                            FamilyPlacementType.CurveBasedDetail)
                .ToList();

            log.Log($"Line families: {list.Count}");
            return new ObservableCollection<FamilySymbol>(list);
        }

        public static void PlaceDetailItems(
            Document doc,
            View view,
            FamilySymbol symbol,
            List<DrainPath> paths,
            UiLogService log)
        {
            if (symbol == null || paths.Count == 0)
                return;

            using Transaction tx =
                new Transaction(doc, "Place Creaser Items");

            tx.Start();

            if (!symbol.IsActive)
            {
                symbol.Activate();
                doc.Regenerate();
            }

            foreach (var p in paths)
                foreach (var l in p.Lines)
                    doc.Create.NewFamilyInstance(l, symbol, view);

            tx.Commit();
        }
    }
}
