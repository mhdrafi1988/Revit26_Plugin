using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V313.Constants;
using Revit26_Plugin.APUS_V313.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V313.Services
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
            // Use a single collector for better performance
            var collector = new FilteredElementCollector(_doc);

            // Collect ALL sheets first (this is usually a small list)
            var allSheets = collector
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .ToList();

            // Create view-sheet mapping
            var sheetViewMap = new Dictionary<ElementId, ViewSheet>();
            foreach (var sheet in allSheets)
            {
                // GetAllPlacedViews is efficient - it returns cached data
                foreach (var viewId in sheet.GetAllPlacedViews())
                {
                    if (!sheetViewMap.ContainsKey(viewId))
                        sheetViewMap.Add(viewId, sheet);
                }
            }

            // Clear collector and reuse for sections
            collector = new FilteredElementCollector(_doc);

            // Collect section views with optimized filtering
            var sections = collector
                .OfClass(typeof(ViewSection))
                .Cast<ViewSection>()
                .Where(v =>
                    !v.IsTemplate &&
                    v.ViewType == ViewType.Section &&
                    v.GetPrimaryViewId() == ElementId.InvalidElementId)
                .ToList();

            // Build results
            var result = new List<SectionItemViewModel>(sections.Count);

            foreach (var section in sections)
            {
                sheetViewMap.TryGetValue(section.Id, out var sheet);

                bool isPlaced = sheet != null;
                string sheetNumber = isPlaced ? sheet.SheetNumber : string.Empty;

                string scope = section.LookupParameter(ParameterNames.PlacementScope)?.AsString();

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