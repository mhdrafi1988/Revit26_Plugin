// File: GridPlacementHandler.cs
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
    public class GridPlacementHandler : IExternalEventHandler
    {
        public AutoPlaceSectionsViewModel ViewModel { get; set; }
        public List<SectionItemViewModel> SectionsToPlace { get; set; }

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc?.Document;

            if (doc == null)
            {
                ViewModel?.LogError("No active document.");
                ViewModel?.OnPlacementComplete(false, "No active document.");
                return;
            }

            try
            {
                // Collect selected sections
                var selected = SectionsToPlace ?? ViewModel.Sections.Where(x => x.IsSelected).ToList();

                if (!selected.Any())
                {
                    ViewModel.LogWarning("No sections selected.");
                    ViewModel.OnPlacementComplete(false, "No sections selected");
                    return;
                }

                // Apply UI filters
                var filtered = SectionFilterService.Apply(
                    selected,
                    ViewModel.SelectedPlacementScope,
                    ViewModel.SelectedPlacementState,
                    ViewModel.SheetNumberFilter);

                if (!filtered.Any())
                {
                    ViewModel.LogWarning("No sections passed filtering.");
                    ViewModel.OnPlacementComplete(false, "No sections passed filtering");
                    return;
                }

                // Spatial sorting
                View referenceView = uidoc.ActiveView;
                if (referenceView == null)
                {
                    ViewModel.LogError("No active view for spatial sorting.");
                    ViewModel.OnPlacementComplete(false, "No active view");
                    return;
                }

                double yToleranceMm = ViewModel.YToleranceMm;
                var sorted = SectionSortingService
                    .SortLeftToRight(filtered, referenceView, yToleranceMm)
                    .Select(x => x.Item)
                    .ToList();

                // Initialize progress
                ViewModel.Progress.Reset(sorted.Count);

                // TRANSACTION
                using Transaction tx = new Transaction(doc, "APUS V314 – Grid Placement");
                tx.Start();

                try
                {
                    // Resolve title block
                    if (ViewModel.SelectedTitleBlock == null)
                    {
                        ViewModel.LogError("No title block selected.");
                        tx.RollBack();
                        ViewModel.OnPlacementComplete(false, "No title block");
                        return;
                    }

                    FamilySymbol titleBlock = doc.GetElement(ViewModel.SelectedTitleBlock.SymbolId) as FamilySymbol;
                    if (titleBlock == null)
                    {
                        ViewModel.LogError("Selected title block is invalid.");
                        tx.RollBack();
                        ViewModel.OnPlacementComplete(false, "Invalid title block");
                        return;
                    }

                    // Calculate placement area
                    var sheetCreator = new SheetCreationService(doc);
                    ViewSheet tempSheet = sheetCreator.Create(titleBlock, 0);

                    SheetPlacementArea placementArea = SheetLayoutService.Calculate(
                        tempSheet,
                        ViewModel.LeftMarginMm,
                        ViewModel.RightMarginMm,
                        ViewModel.TopMarginMm,
                        ViewModel.BottomMarginMm);

                    doc.Delete(tempSheet.Id);

                    // Read grid gaps
                    double horizontalGapMm = ViewModel.HorizontalGapMm;
                    double verticalGapMm = ViewModel.VerticalGapMm;

                    // Grid-based placement
                    var placer = new MultiSheetGridPlacementService(doc);
                    int placedCount = 0;
                    int failedCount = 0;
                    HashSet<string> sheetNumbers = new HashSet<string>();

                    placer.Place(
                        sorted,
                        titleBlock,
                        placementArea,
                        horizontalGapMm,
                        verticalGapMm,
                        ViewModel,
                        ref placedCount,
                        ref failedCount,
                        ref sheetNumbers);

                    tx.Commit();

                    // UI Logging
                    if (sheetNumbers.Any())
                    {
                        string sheetList = string.Join(", ", sheetNumbers.OrderBy(s => s));
                        ViewModel.LogInfo($"Sheets used: {sheetList}");
                    }

                    ViewModel.LogInfo($"Placement complete: {placedCount} placed, {failedCount} failed");

                    // Open sheets if option is checked
                    if (ViewModel.OpenSheetsAfterPlacement && sheetNumbers.Any())
                    {
                        List<ElementId> sheetIds = new FilteredElementCollector(doc)
                            .OfClass(typeof(ViewSheet))
                            .Cast<ViewSheet>()
                            .Where(sheet => sheetNumbers.Contains(sheet.SheetNumber))
                            .Select(sheet => sheet.Id)
                            .ToList();

                        foreach (ElementId sheetId in sheetIds)
                        {
                            if (doc.GetElement(sheetId) is ViewSheet sheet)
                                uidoc.ActiveView = sheet;
                        }

                        ViewModel.LogInfo($"{sheetIds.Count} sheet(s) opened.");
                    }
                    else if (sheetNumbers.Any())
                    {
                        ViewModel.LogInfo($"{sheetNumbers.Count} sheet(s) created.");
                    }

                    ViewModel.OnPlacementComplete(true, $"{placedCount} sections placed");
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    ViewModel.LogError($"Placement failed: {ex.Message}");
                    ViewModel.OnPlacementComplete(false, ex.Message);
                }
            }
            catch (Exception ex)
            {
                ViewModel.LogError($"Unexpected error: {ex.Message}");
                ViewModel.OnPlacementComplete(false, ex.Message);
            }
        }

        public string GetName() => "APUS V314 – Grid Placement Handler";
    }
}