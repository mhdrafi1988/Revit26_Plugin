using Autodesk.Revit.DB;
using System.Collections.ObjectModel;
using System.Linq;
using Revit26_Plugin.DtlLineDim_V03.Models;

namespace Revit26_Plugin.DtlLineDim_V03.Services
{
    public static class DimensionTypeService
    {
        public static void PopulateAlignedDimensionTypes(
            Document doc,
            ObservableCollection<ComboItem> target)
        {
            target.Clear();

            var types = new FilteredElementCollector(doc)
                .OfClass(typeof(DimensionType))
                .Cast<DimensionType>()
                .Where(t => t.StyleType == DimensionStyleType.Linear)
                .OrderBy(t => t.Name);

            foreach (var t in types)
                target.Add(new ComboItem(t.Name, t.Id));
        }
    }
}
