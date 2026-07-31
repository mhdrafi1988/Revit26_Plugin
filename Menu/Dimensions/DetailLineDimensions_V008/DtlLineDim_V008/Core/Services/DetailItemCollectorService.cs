using Autodesk.Revit.DB;
using System.Collections.ObjectModel;
using System.Linq;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.DtlLineDim.V008.Core.Services
{
    /// <summary>
    /// Collects unique line-based detail-item family symbols visible in the active view.
    /// </summary>
    public static class DetailItemCollectorService
    {
        public static void PopulateDetailItemTypes(
            Document doc,
            View view,
            ObservableCollection<ComboItem> target,
            ObservableCollection<LogEntry> log)
        {
            log.Add(new LogEntry(LogLevel.Info, "Collecting line-based detail item types..."));

            target.Clear();

            var symbols = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .WhereElementIsNotElementType()
                .Cast<FamilyInstance>()
                .Where(fi =>
                    fi.Symbol.Family.FamilyPlacementType == FamilyPlacementType.CurveBasedDetail &&
                    fi.Location is LocationCurve lc &&
                    lc.Curve is Line)
                .GroupBy(fi => fi.Symbol.Id)   // unique symbols only
                .Select(g => g.First().Symbol)
                .OrderBy(s => s.Family.Name)
                .ThenBy(s => s.Name)
                .ToList();

            foreach (var s in symbols)
                target.Add(new ComboItem($"{s.Family.Name} : {s.Name}", s.Id));

            log.Add(new LogEntry(LogLevel.Info, $"Detail Item Types loaded: {symbols.Count}"));
        }
    }
}
