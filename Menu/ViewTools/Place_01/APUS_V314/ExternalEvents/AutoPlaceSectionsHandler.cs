// File: AutoPlaceSectionsHandler.cs
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V314.Models;
using Revit26_Plugin.APUS_V314.Services;
using Revit26_Plugin.APUS_V314.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V314.ExternalEvents
{
    public class AutoPlaceSectionsHandler : IExternalEventHandler
    {
        public AutoPlaceSectionsViewModel ViewModel { get; set; }
        public List<SectionItemViewModel> SectionsToPlace { get; set; }
        public PlacementAlgorithm Algorithm { get; set; } = PlacementAlgorithm.Grid;

        public void Execute(UIApplication app)
        {
            try
            {
                UIDocument uidoc = app.ActiveUIDocument;
                Document doc = uidoc?.Document;

                if (doc == null)
                {
                    ViewModel.LogError("No active document.");
                    ViewModel.OnPlacementComplete(false, "No active document.");
                    return;
                }

                if (ViewModel.Progress.IsCancelled)
                {
                    ViewModel.OnPlacementComplete(false, "Operation cancelled");
                    return;
                }

                // Filter out already placed views if option is set
                var sectionsToProcess = ViewModel.SkipPlacedViews
                    ? SectionsToPlace.Where(x => !x.IsPlaced).ToList()
                    : SectionsToPlace.ToList();

                if (!sectionsToProcess.Any())
                {
                    ViewModel.LogWarning("No sections to place after filtering.");
                    ViewModel.OnPlacementComplete(true, "No sections needed placement");
                    return;
                }

                // Execute based on selected algorithm
                bool success = Algorithm switch
                {
                    PlacementAlgorithm.Grid => ExecuteGridPlacement(doc, uidoc, sectionsToProcess),
                    PlacementAlgorithm.BinPacking => ExecuteBinPackingPlacement(doc, uidoc, sectionsToProcess),
                    PlacementAlgorithm.Ordered => ExecuteOrderedPlacement(doc, uidoc, sectionsToProcess),
                    PlacementAlgorithm.AdaptiveGrid => ExecuteAdaptiveGridPlacement(doc, uidoc, sectionsToProcess),
                    _ => ExecuteGridPlacement(doc, uidoc, sectionsToProcess)
                };

                ViewModel.OnPlacementComplete(success, success ? "Placement completed" : "Placement failed");
            }
            catch (Exception ex)
            {
                ViewModel.LogError($"Unexpected error: {ex.Message}");
                ViewModel.LogError($"Stack trace: {ex.StackTrace}");
                ViewModel.OnPlacementComplete(false, ex.Message);
            }
        }

        private bool ExecuteGridPlacement(Document doc, UIDocument uidoc, List<SectionItemViewModel> sections)
        {
            ViewModel.LogInfo($"Starting Grid placement for {sections.Count} sections");

            using Transaction tx = new Transaction(doc, "APUS V314 – Grid Placement");
            tx.Start();

            try
            {
                // Check title block
                if (ViewModel.SelectedTitleBlock == null)
                {
                    ViewModel.LogError("No title block selected.");
                    tx.RollBack();
                    return false;
                }

                FamilySymbol titleBlock = doc.GetElement(ViewModel.SelectedTitleBlock.SymbolId) as FamilySymbol;
                if (titleBlock == null)
                {
                    ViewModel.LogError("Selected title block is invalid.");
                    tx.RollBack();
                    return false;
                }

                // Create temp sheet to calculate area
                var sheetCreator = new SheetCreationService(doc);
                ViewSheet tempSheet = sheetCreator.Create(titleBlock, 0);

                SheetPlacementArea placementArea = SheetLayoutService.Calculate(
                    tempSheet,
                    ViewModel.LeftMarginMm,
                    ViewModel.RightMarginMm,
                    ViewModel.TopMarginMm,
                    ViewModel.BottomMarginMm);

                doc.Delete(tempSheet.Id);

                // Filter and sort sections
                var referenceView = uidoc.ActiveView;
                if (referenceView == null)
                {
                    ViewModel.LogError("No active view for spatial sorting.");
                    tx.RollBack();
                    return false;
                }

                var filtered = SectionFilterService.Apply(
                    sections,
                    ViewModel.SelectedPlacementScope,
                    ViewModel.SelectedPlacementState,
                    ViewModel.SheetNumberFilter);

                var sorted = SectionSortingService
                    .SortLeftToRight(
                        filtered,
                        referenceView,
                        ViewModel.YToleranceMm)
                    .Select(x => x.Item)
                    .ToList();

                if (!sorted.Any())
                {
                    ViewModel.LogWarning("No sections passed filtering.");
                    tx.RollBack();
                    return false;
                }

                // Initialize progress
                ViewModel.Progress.Reset(sorted.Count);

                // Execute placement
                var placementService = new MultiSheetGridPlacementService(doc);
                int placedCount = 0;
                int failedCount = 0;
                HashSet<string> sheetNumbers = new HashSet<string>();

                placementService.Place(
                    sorted,
                    titleBlock,
                    placementArea,
                    ViewModel.HorizontalGapMm,
                    ViewModel.VerticalGapMm,
                    ViewModel,
                    ref placedCount,
                    ref failedCount,
                    ref sheetNumbers);

                tx.Commit();

                // Log results
                if (sheetNumbers.Any())
                {
                    string sheetList = string.Join(", ", sheetNumbers.OrderBy(s => s));
                    ViewModel.LogInfo($"Sheets created: {sheetList}");
                }

                ViewModel.LogInfo($"Grid placement complete: {placedCount} placed, {failedCount} failed");
                return true;
            }
            catch (Exception ex)
            {
                tx.RollBack();
                ViewModel.LogError($"Grid placement failed: {ex.Message}");
                return false;
            }
        }

        private bool ExecuteBinPackingPlacement(Document doc, UIDocument uidoc, List<SectionItemViewModel> sections)
        {
            ViewModel.LogInfo($"Starting Bin Packing placement for {sections.Count} sections");
            // Implementation would go here
            ViewModel.LogInfo("Bin Packing algorithm - To be implemented");
            return true;
        }

        private bool ExecuteOrderedPlacement(Document doc, UIDocument uidoc, List<SectionItemViewModel> sections)
        {
            ViewModel.LogInfo($"Starting Ordered placement for {sections.Count} sections");
            // Implementation would go here
            ViewModel.LogInfo("Ordered algorithm - To be implemented");
            return true;
        }

        private bool ExecuteAdaptiveGridPlacement(Document doc, UIDocument uidoc, List<SectionItemViewModel> sections)
        {
            ViewModel.LogInfo($"Starting Adaptive Grid placement for {sections.Count} sections");
            // Implementation would go here
            ViewModel.LogInfo("Adaptive Grid algorithm - To be implemented");
            return true;
        }

        public string GetName() => $"APUS V314 – {Algorithm} Placement Handler";
    }
}