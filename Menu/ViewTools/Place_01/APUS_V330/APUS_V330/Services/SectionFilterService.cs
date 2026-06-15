// File: Services/SectionFilterService.cs
using Revit26_Plugin.APUS_V330.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V330.Services
{
    public static class SectionFilterService
    {
        private const string ALL = "All";

        public static List<SectionItemViewModel> Apply(
            IEnumerable<SectionItemViewModel> source,
            string placementScope,
            string placementState,
            string sheetNumber)
        {
            if (source == null) return new List<SectionItemViewModel>();

            IEnumerable<SectionItemViewModel> result = source;

            if (!string.IsNullOrWhiteSpace(placementScope) &&
                !string.Equals(placementScope, ALL, StringComparison.OrdinalIgnoreCase))
            {
                result = result.Where(x =>
                    !string.IsNullOrWhiteSpace(x.PlacementScope) &&
                    x.PlacementScope.Contains(placementScope, StringComparison.OrdinalIgnoreCase));
            }

            result = placementState switch
            {
                "Placed Only"   => result.Where(x =>  x.IsPlaced),
                "Unplaced Only" => result.Where(x => !x.IsPlaced),
                _               => result
            };

            if (!string.IsNullOrWhiteSpace(sheetNumber) &&
                !string.Equals(sheetNumber, ALL, StringComparison.OrdinalIgnoreCase))
            {
                result = result.Where(x =>
                    x.IsPlaced &&
                    !string.IsNullOrWhiteSpace(x.SheetNumber) &&
                    x.SheetNumber.Contains(sheetNumber, StringComparison.OrdinalIgnoreCase));
            }

            return result.ToList();
        }
    }
}
