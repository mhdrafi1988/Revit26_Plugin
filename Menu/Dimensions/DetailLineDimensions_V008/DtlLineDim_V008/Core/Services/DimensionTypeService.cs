using Autodesk.Revit.DB;
using System.Collections.ObjectModel;
using System.Linq;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.DtlLineDim.V008.Core.Services
{
    /// <summary>
    /// Collects Linear-style DimensionTypes available in the document.
    /// </summary>
    public static class DimensionTypeService
    {
        public static void PopulateAlignedDimensionTypes(
            Document doc,
            ObservableCollection<ComboItem> target,
            ObservableCollection<LogEntry> log)
        {
            log.Add(new LogEntry(LogLevel.Info, "Collecting linear dimension types..."));

            target.Clear();

            var types = new FilteredElementCollector(doc)
                .OfClass(typeof(DimensionType))
                .Cast<DimensionType>()
                .Where(t => t.StyleType == DimensionStyleType.Linear)
                .OrderBy(t => t.Name)
                .ToList();

            foreach (var t in types)
                target.Add(new ComboItem(t.Name, t.Id));

            log.Add(new LogEntry(LogLevel.Info, $"Dimension Types loaded: {types.Count}"));
        }
    }
}
