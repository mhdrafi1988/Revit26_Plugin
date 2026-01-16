using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V306.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V306.Services
{
    public class SectionCollectionService
    {
        private readonly Document _doc;

        public SectionCollectionService(Document doc)
        {
            _doc = doc;
        }

        public List<SectionItemViewModel> Collect()
        {
            var sections = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSection))
                .Cast<ViewSection>()
                .Where(v =>
                    !v.IsTemplate &&
                    v.ViewType == ViewType.Section &&
                    v.GetPrimaryViewId() == ElementId.InvalidElementId) // always exclude dependents
                .ToList();

            var viewports = new FilteredElementCollector(_doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .ToList();

            return sections.Select(v =>
            {
                var vp = viewports.FirstOrDefault(x => x.ViewId == v.Id);
                bool placed = vp != null;

                string sheetNo = placed
                    ? (_doc.GetElement(vp.SheetId) as ViewSheet)?.SheetNumber
                    : null;

                string scope = v.LookupParameter("Placement_Scope")?.AsString();

                return new SectionItemViewModel(v, placed, sheetNo, scope);
            }).ToList();
        }
    }
}
