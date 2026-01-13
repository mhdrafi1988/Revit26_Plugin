using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.copv2.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.copv2.Helpers
{
    public static class RevitHelper
    {
        // ---------------------------------------------------------------------
        // SECTION VIEWS (with sheet placement info)
        // ---------------------------------------------------------------------
        public static List<CalloutViewModelCall> GetSectionViews(Document doc)
        {
            var result = new List<CalloutViewModelCall>();

            // All viewports in project
            var viewports = new FilteredElementCollector(doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .ToList();

            // All section views in project
            var sections = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSection))
                .Cast<ViewSection>()
                .Where(v => !v.IsTemplate)
                .ToList();

            foreach (var v in sections)
            {
                var vp = viewports.FirstOrDefault(x => x.ViewId == v.Id);

                bool placed = vp != null;
                string sheetName = "";
                string sheetNumber = "";
                ElementId sheetId = ElementId.InvalidElementId;
                string detailNum = "";

                if (placed)
                {
                    var sheet = doc.GetElement(vp.SheetId) as ViewSheet;
                    if (sheet != null)
                    {
                        sheetName = sheet.Name;
                        sheetNumber = sheet.SheetNumber;
                        sheetId = sheet.Id;
                        detailNum = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER)?.AsString();
                    }
                }

                result.Add(new CalloutViewModelCall
                {
                    SectionName = v.Name,
                    IsPlaced = placed,
                    SheetName = sheetName,
                    SheetNumber = sheetNumber,
                    SheetId = sheetId,
                    DetailNumber = detailNum,
                    ViewId = v.Id,
                    IsVisible = true
                });
            }

            return result;
        }

        // ---------------------------------------------------------------------
        // DRAFTING VIEWS
        // ---------------------------------------------------------------------
        public static List<View> GetDraftingViews(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewDrafting))
                .Cast<View>()
                .Where(v => !v.IsTemplate)
                .OrderBy(v => v.Name)
                .ToList();
        }

        // ---------------------------------------------------------------------
        // ALL SHEETS (for dropdown filter)
        // ---------------------------------------------------------------------
        public static List<ViewSheet> GetAllSheets(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsTemplate)
                .OrderBy(s => s.SheetNumber)
                .ToList();
        }

        // ---------------------------------------------------------------------
        // GET ACTIVE SHEET (for "default sheet" behavior)
        // ---------------------------------------------------------------------
        public static ElementId GetActiveSheetId(Document doc, UIDocument uidoc)
        {
            var view = uidoc.ActiveView as ViewSheet;
            return view?.Id ?? ElementId.InvalidElementId;
        }
    }
}
