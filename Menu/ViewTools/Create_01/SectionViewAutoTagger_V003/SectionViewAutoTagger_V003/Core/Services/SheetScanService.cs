using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.SectionViewAutoTagger.V003
{
    /// <summary>
    /// Reads sheets and the section views placed on them via Viewports.
    /// Read-only queries — safe to call outside a transaction.
    /// </summary>
    public class SheetScanService
    {
        /// <summary>All sheets in the document, ordered by SheetNumber.</summary>
        public List<SheetOption> GetAllSheets(Document doc)
        {
            var sheets = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => !s.IsPlaceholder)
                .OrderBy(s => s.SheetNumber)
                .Select(s => new SheetOption(s.SheetNumber, s.Name, s.Id))
                .ToList();

            return sheets;
        }

        /// <summary>
        /// Section views (ViewType.Section) placed as viewports on the given sheet.
        /// ASSUMPTION: "placed on a sheet" means an actual Viewport instance exists
        /// on that sheet referencing the section view — not merely referenced by a
        /// callout/reference tag elsewhere.
        /// </summary>
        public List<SectionViewOption> GetSectionViewsOnSheet(Document doc, ElementId sheetId)
        {
            var viewports = new FilteredElementCollector(doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .Where(vp => vp.SheetId == sheetId);

            var result = new List<SectionViewOption>();

            foreach (var vp in viewports)
            {
                if (doc.GetElement(vp.ViewId) is View view && view.ViewType == ViewType.Section)
                {
                    result.Add(new SectionViewOption(view.Id, view.Name));
                }
            }

            return result.OrderBy(v => v.ViewName).ToList();
        }
    }
}
