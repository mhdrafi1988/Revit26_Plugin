using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit22_Plugin.callout.Models;
using Revit22_Plugin.callout.Commands;
using Revit22_Plugin.callout.Helpers;
//using Revit22_Plugin.callout.Models;


namespace Revit22_Plugin.callout.Helpers
{
    public static class RevitHelper
    {
        public static List<CalloutViewModelCall> GetSectionViews(Document doc)
        {
            var sections = new List<CalloutViewModelCall>();
            var viewports = new FilteredElementCollector(doc).OfClass(typeof(Viewport)).Cast<Viewport>().ToList();
            var sectionViews = new FilteredElementCollector(doc).OfClass(typeof(ViewSection)).Cast<ViewSection>();

            foreach (var view in sectionViews)
            {
                if (view.IsTemplate) continue;

                var vp = viewports.FirstOrDefault(v => v.ViewId == view.Id);
                bool isPlaced = vp != null;
                string sheetName = isPlaced ? (doc.GetElement(vp.SheetId) as ViewSheet)?.Name : "";
                string sheetNumber = isPlaced ? (doc.GetElement(vp.SheetId) as ViewSheet)?.SheetNumber : "";
                string detailNumber = isPlaced ? vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER)?.AsString() : "";

                sections.Add(new CalloutViewModelCall
                {
                    SectionName = view.Name,
                    SheetName = sheetName,
                    SheetNumber = sheetNumber,
                    DetailNumber = detailNumber,
                    ViewId = view.Id,
                    IsPlaced = isPlaced
                });
            }

            return sections;
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
    }
}
