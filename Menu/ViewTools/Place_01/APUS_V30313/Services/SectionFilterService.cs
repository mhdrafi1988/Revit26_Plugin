using Revit26_Plugin.APUS_V313.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V313.Services
{
    /// <summary>
    /// Applies UI-driven filters to section items.
    /// IMPORTANT:
    /// - Treats "All" as NO FILTER (critical fix).
    /// - Matches old V311 behavior exactly.
    /// </summary>
    public static class SectionFilterService
    {
        private const string ALL = "All";

        public static List<SectionItemViewModel> Apply(
            IEnumerable<SectionItemViewModel> source,
            string placementScope,
            string placementState,
            string sheetNumber)
        {
            if (source == null)
                return new List<SectionItemViewModel>();

            IEnumerable<SectionItemViewModel> result = source;

            // --------------------------------------------------
            // 1. Placement Scope filter
            // --------------------------------------------------
            if (!string.IsNullOrWhiteSpace(placementScope) &&
                !string.Equals(placementScope, ALL, StringComparison.OrdinalIgnoreCase))
            {
                result = result.Where(x =>
                    !string.IsNullOrWhiteSpace(x.PlacementScope) &&
                    x.PlacementScope.Contains(
                        placementScope,
                        StringComparison.OrdinalIgnoreCase));
            }

            // --------------------------------------------------
            // 2. Placement State filter
            // --------------------------------------------------
            result = placementState switch
            {
                "Placed Only" => result.Where(x => x.IsPlaced),
                "Unplaced Only" => result.Where(x => !x.IsPlaced),
                _ => result // "All" or null
            };

            // --------------------------------------------------
            // 3. Sheet Number filter (placed views only)
            // --------------------------------------------------
            if (!string.IsNullOrWhiteSpace(sheetNumber) &&
                !string.Equals(sheetNumber, ALL, StringComparison.OrdinalIgnoreCase))
            {
                result = result.Where(x =>
                    x.IsPlaced &&
                    !string.IsNullOrWhiteSpace(x.SheetNumber) &&
                    x.SheetNumber.Contains(
                        sheetNumber,
                        StringComparison.OrdinalIgnoreCase));
            }

            return result.ToList();
        }
    }
}