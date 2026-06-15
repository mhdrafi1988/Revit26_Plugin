using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CalloutCOP_V06.Helpers
{
    public static class SheetLookupHelper
    {
        public static IReadOnlyList<string> GetSheetNumbers(Document doc, View view)
        {
            var result = new HashSet<string>();

            var viewports = new FilteredElementCollector(doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>();

            foreach (var vp in viewports)
            {
                if (vp.ViewId != view.Id)
                    continue;

                if (doc.GetElement(vp.SheetId) is ViewSheet sheet)
                    result.Add(sheet.SheetNumber);
            }

            return result.ToList();
        }
    }
}
