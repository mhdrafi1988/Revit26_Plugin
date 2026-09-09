// File: SectionFilterService.cs
using Revit26_Plugin.APUS.V322.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS.V322.Services
{
    /// <summary>
    /// Filter states for the 3-way All / Placed / Unplaced toggle.
    /// </summary>
    public enum PlacementFilterState
    {
        All,
        PlacedOnly,
        UnplacedOnly
    }

    /// <summary>
    /// V321 filtering: name search + All/Placed/Unplaced toggle.
    /// The old free-text "Placement Scope" filter is removed — Rafi confirmed
    /// scope is fixed to Whole Model, so there's nothing left to filter by
    /// scope on. Sheet-number filtering is dropped for the same reason (it
    /// existed to narrow scope, which no longer varies).
    /// </summary>
    public static class SectionFilterService
    {
        public static List<SectionItemViewModel> Apply(
            IEnumerable<SectionItemViewModel> source,
            string nameSearch,
            PlacementFilterState placementState)
        {
            if (source == null)
                return new List<SectionItemViewModel>();

            IEnumerable<SectionItemViewModel> result = source;

            if (!string.IsNullOrWhiteSpace(nameSearch))
            {
                result = result.Where(x =>
                    !string.IsNullOrWhiteSpace(x.ViewName) &&
                    x.ViewName.Contains(nameSearch, StringComparison.OrdinalIgnoreCase));
            }

            result = placementState switch
            {
                PlacementFilterState.PlacedOnly => result.Where(x => x.IsPlaced),
                PlacementFilterState.UnplacedOnly => result.Where(x => !x.IsPlaced),
                _ => result
            };

            return result.ToList();
        }
    }
}
