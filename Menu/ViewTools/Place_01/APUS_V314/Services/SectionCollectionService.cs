// File: SectionCollectionService.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V314.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V314.Services
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
            // Collect all sheets and their placed views
            var sheetViewMap = new Dictionary<ElementId, ViewSheet>();

            var sheets = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>();

            foreach (var sheet in sheets)
            {
                foreach (var viewId in sheet.GetAllPlacedViews())
                {
                    if (!sheetViewMap.ContainsKey(viewId))
                        sheetViewMap.Add(viewId, sheet);
                }
            }

            // Collect section views
            var sections = new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSection))
                .Cast<ViewSection>()
                .Where(v =>
                    !v.IsTemplate &&
                    v.ViewType == ViewType.Section &&
                    v.GetPrimaryViewId() == ElementId.InvalidElementId)
                .ToList();

            // Build ViewModels
            var result = new List<SectionItemViewModel>();

            foreach (var section in sections)
            {
                sheetViewMap.TryGetValue(section.Id, out var sheet);

                bool isPlaced = sheet != null;
                string sheetNumber = isPlaced ? sheet.SheetNumber : string.Empty;

                string scope = section.LookupParameter("Placement_Scope")?.AsString();

                result.Add(
                    new SectionItemViewModel(
                        section,
                        isPlaced,
                        sheetNumber,
                        scope));
            }

            return result;
        }
    }
}