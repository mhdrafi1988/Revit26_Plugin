using Autodesk.Revit.DB;
using Revit26_Plugin.AutoLiner_V02.ViewModels;
using Revit26_Plugin.AutoLiner_V02.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.AutoLiner_V02.Services
{
    public static class DetailItemCollectorService
    {
        public static IList<DetailItemOption> Collect(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .Cast<FamilySymbol>()
                .Where(s =>
                    s.Family.IsParametric &&
                    s.get_Parameter(BuiltInParameter.FAMILY_LINE_LENGTH_PARAM) != null)
                .GroupBy(s => s.Id)
                .Select(g => new DetailItemOption(g.First()))
                .OrderBy(o => o.Name)
                .ToList();
        }
    }
}
