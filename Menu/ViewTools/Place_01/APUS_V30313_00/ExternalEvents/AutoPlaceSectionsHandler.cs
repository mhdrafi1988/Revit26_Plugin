using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V313.Models;
using Revit26_Plugin.APUS_V313.Services;
using Revit26_Plugin.APUS_V313.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V313.Commands
{
    public class AutoPlaceSectionsHandler
    {
        private readonly Document _doc;
        private readonly UIDocument _uiDoc;

        public AutoPlaceSectionsHandler(UIDocument uiDoc)
        {
            _uiDoc = uiDoc;
            _doc = uiDoc.Document;
        }

        public void Execute(AutoPlaceSectionsViewModel vm)
        {
            if (vm == null)
                throw new ArgumentNullException(nameof(vm));

            // --------------------------------------------------
            // 1. Validate input
            // --------------------------------------------------
            var selectedSections = vm.Sections
                .Where(s => s.IsSelected)
                .ToList();

            if (selectedSections.Count == 0)
            {
                vm.LogWarning("No sections selected.");
                return;
            }

            if (vm.SelectedTitleBlock == null)
            {
                vm.LogError("No title block selected.");
                return;
            }

            // --------------------------------------------------
            // 2. Spatial sort (HUMAN reading order)
            // --------------------------------------------------
            IList<SectionItemViewModel> orderedSections =
                SectionSpatialSortingService.SortByReadingOrder(
                    selectedSections,
                    vm.RowToleranceMm);

            if (orderedSections.Count == 0)
            {
                vm.LogWarning("Spatial sorting returned no sections.");
                return;
            }

            // --------------------------------------------------
            // 3. Prepare placement services
            // --------------------------------------------------
            var placementArea = vm.PlacementArea; // SheetPlacementArea
            var placer = new MultiSheetShelfPlacementService(_doc);

            int placedCount = 0;
            int failedCount = 0;

            // --------------------------------------------------
            // 4. Place sections (transaction)
            // --------------------------------------------------
            using (Transaction t = new Transaction(_doc, "Auto Place Sections"))
            {
                t.Start();

                placer.Place(
                    orderedSections,
                    vm.SelectedTitleBlock,
                    placementArea,
                    vm.HorizontalGapMm,
                    vm.VerticalGapMm,
                    ref placedCount,
                    ref failedCount);

                t.Commit();
            }

            // --------------------------------------------------
            // 5. Report result
            // --------------------------------------------------
            vm.LogInfo($"Placement complete.");
            vm.LogInfo($"Placed views: {placedCount}");
            vm.LogInfo($"Failed views: {failedCount}");
        }
    }
}
