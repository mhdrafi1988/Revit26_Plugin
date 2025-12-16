using Autodesk.Revit.DB;
using System.Collections.ObjectModel;
using System.Linq;
using Revit26_Plugin.DtlLineDim_V03.Models;

namespace Revit26_Plugin.DtlLineDim_V03.Services
{
    public static class DetailItemCollectorService
    {
        public static void PopulateDetailItemTypes(
            Document doc,
            View view,
            ObservableCollection<ComboItem> target,
            ObservableCollection<string> log)
        {
            target.Clear();

            var symbols = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .Where(fi =>
                    fi.Symbol.Family.FamilyPlacementType == FamilyPlacementType.CurveBasedDetail &&
                    fi.Location is LocationCurve lc &&
                    lc.Curve is Line)
                .GroupBy(fi => fi.Symbol.Id)   // unique types
                .Select(g => g.First().Symbol)
                .OrderBy(s => s.Family.Name)
                .ThenBy(s => s.Name)
                .ToList();

            foreach (var s in symbols)
                target.Add(new ComboItem($"{s.Family.Name} : {s.Name}", s.Id));

            log.Insert(0, $"Two-point line-based detail types loaded: {symbols.Count}");
        }
    }
}
