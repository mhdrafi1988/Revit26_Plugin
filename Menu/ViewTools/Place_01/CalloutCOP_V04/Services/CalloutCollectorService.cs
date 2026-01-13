using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.CalloutCOP_V04.Models;

namespace Revit26_Plugin.CalloutCOP_V04.Services
{
    public static class CalloutCollectorService
    {
        public static IList<ViewSheet> GetSheets(Document doc)
            => new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsTemplate)
                .OrderBy(s => s.SheetNumber)
                .ToList();

        public static IList<ViewDrafting> GetDraftingViews(Document doc)
            => new FilteredElementCollector(doc)
                .OfClass(typeof(ViewDrafting))
                .Cast<ViewDrafting>()
                .Where(v => !v.IsTemplate)
                .OrderBy(v => v.Name)
                .ToList();

        public static IList<CalloutItem> GetSectionItems(Document doc)
        {
            var viewports = new FilteredElementCollector(doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .ToList();

            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSection))
                .Cast<ViewSection>()
                .Where(v => !v.IsTemplate)
                .Select(sec =>
                {
                    var vp = viewports.FirstOrDefault(v => v.ViewId == sec.Id);
                    var placed = vp != null;

                    var sheet = placed ? doc.GetElement(vp.SheetId) as ViewSheet : null;

                    return new CalloutItem
                    {
                        ViewId = sec.Id,
                        SheetId = vp?.SheetId ?? ElementId.InvalidElementId,
                        SectionName = sec.Name,
                        IsPlaced = placed,
                        SheetNumber = sheet?.SheetNumber ?? "",
                        DetailNumber = vp?.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER)?.AsString()
                    };
                })
                .ToList();
        }
    }
}
