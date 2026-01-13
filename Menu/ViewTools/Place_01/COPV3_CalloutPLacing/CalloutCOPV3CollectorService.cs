using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;
using Revit22_Plugin.copv3.Models;

namespace Revit22_Plugin.copv3.Services
{
    public static class CalloutCOPV3CollectorService
    {
        public static List<ViewSheet> GetSheets(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsTemplate)
                .OrderBy(s => s.SheetNumber)
                .ToList();
        }

        public static List<View> GetDraftingViews(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewDrafting))
                .Cast<View>()
                .Where(v => !v.IsTemplate)
                .OrderBy(v => v.Name)
                .ToList();
        }

        public static List<CalloutCOPV3Item> GetSectionItems(Document doc)
        {
            var list = new List<CalloutCOPV3Item>();

            var viewports = new FilteredElementCollector(doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .ToList();

            var sections = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSection))
                .Cast<ViewSection>()
                .Where(v => !v.IsTemplate)
                .ToList();

            foreach (var sec in sections)
            {
                var vp = viewports.FirstOrDefault(v => v.ViewId == sec.Id);

                bool placed = vp != null;
                string sheetNum = "";
                string detailNum = "";
                ElementId sheetId = ElementId.InvalidElementId;

                if (placed)
                {
                    sheetId = vp.SheetId;
                    ViewSheet sh = doc.GetElement(sheetId) as ViewSheet;

                    sheetNum = sh?.SheetNumber ?? "";
                    detailNum = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER)?.AsString();
                }

                list.Add(new CalloutCOPV3Item
                {
                    SectionName = sec.Name,
                    SheetNumber = sheetNum,
                    DetailNumber = detailNum,
                    ViewId = sec.Id,
                    SheetId = sheetId,
                    IsPlaced = placed
                });
            }

            return list;
        }
    }
}
