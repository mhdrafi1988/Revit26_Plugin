using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CalloutCOP_V06.Services
{
    public static class ViewSheetLookupService
    {
        public static Dictionary<ElementId, List<string>> Build(Document doc)
        {
            var result = new Dictionary<ElementId, List<string>>();

            var viewports = new FilteredElementCollector(doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>();

            foreach (var vp in viewports)
            {
                if (doc.GetElement(vp.SheetId) is not ViewSheet sheet)
                    continue;

                if (!result.TryGetValue(vp.ViewId, out var list))
                {
                    list = new List<string>();
                    result[vp.ViewId] = list;
                }

                if (!list.Contains(sheet.SheetNumber))
                    list.Add(sheet.SheetNumber);
            }

            return result;
        }
    }
}
