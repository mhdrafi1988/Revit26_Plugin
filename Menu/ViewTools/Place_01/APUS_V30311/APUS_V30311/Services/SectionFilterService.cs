using Revit26_Plugin.APUS_V311.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V311.Services
{
    public static class SectionFilterService
    {
        public static List<SectionItemViewModel> Apply(
            IEnumerable<SectionItemViewModel> source,
            string placementScope,
            string placementState,
            string sheetNumber)
        {
            IEnumerable<SectionItemViewModel> result = source;

            // 1?? Placement Scope
            if (!string.IsNullOrWhiteSpace(placementScope))
            {
                result = result.Where(x =>
                    !string.IsNullOrWhiteSpace(x.PlacementScope) &&
                    x.PlacementScope.Contains(placementScope,
                        System.StringComparison.OrdinalIgnoreCase));
            }

            // 2?? Placement State
            result = placementState switch
            {
                "Placed Only" => result.Where(x => x.IsPlaced),
                "Unplaced Only" => result.Where(x => !x.IsPlaced),
                _ => result
            };

            // 3?? Sheet Number (only applies to placed views)
            if (!string.IsNullOrWhiteSpace(sheetNumber))
            {
                result = result.Where(x =>
                    x.IsPlaced &&
                    !string.IsNullOrWhiteSpace(x.SheetNumber) &&
                    x.SheetNumber.Contains(sheetNumber,
                        System.StringComparison.OrdinalIgnoreCase));
            }

            return result.ToList();
        }
    }
}
