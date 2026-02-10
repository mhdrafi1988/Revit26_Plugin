using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V311.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V311.Services
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
            // Collect ALL section views (no silent exclusions)
            var sections = new FilteredElementCollector(_doc)
    .OfClass(typeof(ViewSection))
    .Cast<ViewSection>()
    .Where(v =>
        !v.IsTemplate &&
        v.ViewType == ViewType.Section &&
        v.GetPrimaryViewId() == ElementId.InvalidElementId
    )
    .ToList();


            var viewports = new FilteredElementCollector(_doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .ToList();

            return sections.Select(v =>
            {
                var vp = viewports.FirstOrDefault(x => x.ViewId == v.Id);
                bool isPlaced = vp != null;

                string sheetNo = isPlaced
                    ? (_doc.GetElement(vp.SheetId) as ViewSheet)?.SheetNumber
                    : null;

                string scope =
                    v.LookupParameter("Placement_Scope")?.AsString();

                return new SectionItemViewModel(
                    v,
                    isPlaced,
                    sheetNo,
                    scope);
            }).ToList();
        }
    }
}
