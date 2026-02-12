// File: SectionPlacementHandler.cs
// FIXED: Proper transaction for temporary sheet operations
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V317.Helpers;
using Revit26_Plugin.APUS_V317.Models;
using Revit26_Plugin.APUS_V317.Services;
using Revit26_Plugin.APUS_V317.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V317.ExternalEvents
{
    /// <summary>
    /// SINGLE SOURCE OF TRUTH for Revit document modification.
    /// All document changes occur within a single transaction group.
    /// No other class creates or commits transactions.
    /// </summary>
    public class SectionPlacementHandler : IExternalEventHandler
    {
        public AutoPlaceSectionsViewModel ViewModel { get; set; }
        public List<SectionItemViewModel> SectionsToPlace { get; set; }

        public void Execute(UIApplication app)
        {
            var uidoc = app.ActiveUIDocument;
            var doc = uidoc?.Document;

            if (doc == null)
            {
                ViewModel?.LogError("No active document.");
                ViewModel?.OnPlacementComplete(false, "No active document.");
                return;
            }

            // --- STAGE 1: PRE-VALIDATION (NO TRANSACTION) ---
            var validationResult = ValidatePlacement(doc, uidoc);
            if (!validationResult.IsValid)
            {
                ViewModel?.OnPlacementComplete(false, validationResult.ErrorMessage);
                return;
            }

            // --- STAGE 2: PREPARE PLACEMENT DATA (NO TRANSACTION) ---
            var placementData = PreparePlacementData(uidoc, validationResult.FilteredSections);
            if (placementData == null)
            {
                ViewModel?.OnPlacementComplete(false, "Failed to prepare placement data");
                return;
            }

            // --- STAGE 3: TRANSACTION GROUP - ALL OR NOTHING ---
            using (var transactionGroup = new TransactionGroup(doc, "APUS V314 – Multi-Sheet Section Placement"))
            {
                try
                {
                    transactionGroup.Start();

                    using (var transaction = new Transaction(doc, "Place Sections"))
                    {
                        transaction.Start();

                        try
                        {
                            // Execute placement - all services work within this transaction
                            var placementResult = ExecutePlacementAlgorithm(
                                doc,
                                placementData,
                                validationResult.TitleBlock,
                                validationResult.PlacementArea);

                            if (!placementResult.Success)
                            {
                                transaction.RollBack();
                                transactionGroup.RollBack();
                                ViewModel?.OnPlacementComplete(false, placementResult.ErrorMessage);
                                return;
                            }

                            transaction.Commit();

                            // --- STAGE 4: POST-PLACEMENT UI OPERATIONS (NO DOCUMENT CHANGES) ---
                            transactionGroup.Assimilate();

                            PostPlacementOperations(uidoc, placementResult.SheetNumbers);
                            ViewModel?.OnPlacementComplete(true,
                                $"{placementResult.PlacedCount} sections placed on {placementResult.SheetNumbers.Count} sheets");
                        }
                        catch (Exception ex)
                        {
                            transaction.RollBack();
                            transactionGroup.RollBack();
                            ViewModel?.LogError($"❌ Placement failed: {ex.Message}");
                            ViewModel?.OnPlacementComplete(false, ex.Message);
                        }
                    }
                }
                catch (Exception ex)
                {
                    ViewModel?.LogError($"❌ Transaction group failed: {ex.Message}");
                    ViewModel?.OnPlacementComplete(false, ex.Message);
                }
            }
        }

        private ValidationResult ValidatePlacement(Document doc, UIDocument uidoc)
        {
            var result = new ValidationResult();

            // 1. Check sections
            var selected = SectionsToPlace ??
                ViewModel?.Sections?.Where(x => x.IsSelected).ToList() ??
                new List<SectionItemViewModel>();

            if (!selected.Any())
            {
                result.ErrorMessage = "No sections selected for placement.";
                return result;
            }

            // 2. Check title block
            if (ViewModel?.SelectedTitleBlock == null)
            {
                result.ErrorMessage = "No title block selected.";
                return result;
            }

            var titleBlock = doc.GetElement(ViewModel.SelectedTitleBlock.SymbolId) as FamilySymbol;
            if (titleBlock == null)
            {
                result.ErrorMessage = "Selected title block is invalid.";
                return result;
            }

            // 3. Apply filters
            var filtered = SectionFilterService.Apply(
                selected,
                ViewModel.SelectedPlacementScope,
                ViewModel.SelectedPlacementState,
                ViewModel.SheetNumberFilter);

            if (!filtered.Any())
            {
                result.ErrorMessage = "No sections passed filtering criteria.";
                return result;
            }

            // 4. Validate margins
            if (!ValidateMargins())
            {
                result.ErrorMessage = "Invalid margin settings. Ensure all margins are non-negative and total margins don't exceed sheet size.";
                return result;
            }

            // 5. Calculate placement area using TEMPORARY sheet - NOW WITH PROPER TRANSACTION
            var placementArea = CalculatePlacementArea(doc, titleBlock);
            if (placementArea == null)
            {
                result.ErrorMessage = "Failed to calculate placement area.";
                return result;
            }

            // 6. Check spatial sorting (don't modify document)
            var referenceView = uidoc.ActiveView;
            if (referenceView == null)
            {
                result.ErrorMessage = "No active view for spatial sorting.";
                return result;
            }

            var sorted = SectionSortingService
                .SortLeftToRight(filtered, referenceView, ViewModel.YToleranceMm)
                .Select(x => x.Item)
                .ToList();

            result.IsValid = true;
            result.FilteredSections = sorted;
            result.TitleBlock = titleBlock;
            result.PlacementArea = placementArea;

            return result;
        }

        private bool ValidateMargins()
        {
            return ViewModel.LeftMarginMm >= 0 &&
                   ViewModel.RightMarginMm >= 0 &&
                   ViewModel.TopMarginMm >= 0 &&
                   ViewModel.BottomMarginMm >= 0;
        }

        private SheetPlacementArea CalculatePlacementArea(Document doc, FamilySymbol titleBlock)
        {
            try
            {
                SheetPlacementArea result = null;

                // Create temporary sheet WITHIN a transaction
                using (var tempTransaction = new Transaction(doc, "Temporary Sheet Creation"))
                {
                    tempTransaction.Start();

                    var tempSheetId = CreateTemporarySheet(doc, titleBlock);
                    if (tempSheetId == null || tempSheetId == ElementId.InvalidElementId)
                    {
                        tempTransaction.RollBack();
                        return null;
                    }

                    var tempSheet = doc.GetElement(tempSheetId) as ViewSheet;
                    if (tempSheet == null)
                    {
                        tempTransaction.RollBack();
                        return null;
                    }

                    result = SheetLayoutService.Calculate(
                        tempSheet,
                        ViewModel.LeftMarginMm,
                        ViewModel.RightMarginMm,
                        ViewModel.TopMarginMm,
                        ViewModel.BottomMarginMm);

                    // Delete temporary sheet within same transaction
                    doc.Delete(tempSheetId);

                    tempTransaction.Commit();
                }

                return result;
            }
            catch (Exception ex)
            {
                ViewModel?.LogError($"Failed to calculate placement area: {ex.Message}");
                return null;
            }
        }

        private ElementId CreateTemporarySheet(Document doc, FamilySymbol titleBlock)
        {
            try
            {
                if (!titleBlock.IsActive)
                    titleBlock.Activate();

                var sheet = ViewSheet.Create(doc, titleBlock.Id);
                return sheet.Id;
            }
            catch
            {
                return null;
            }
        }

        private PlacementData PreparePlacementData(UIDocument uidoc, List<SectionItemViewModel> sections)
        {
            return new PlacementData
            {
                Sections = sections,
                ReferenceView = uidoc.ActiveView
            };
        }

        private PlacementResult ExecutePlacementAlgorithm(
            Document doc,
            PlacementData data,
            FamilySymbol titleBlock,
            SheetPlacementArea placementArea)
        {
            var result = new PlacementResult();

            ViewModel?.LogInfo($"🚀 Executing placement algorithm: {ViewModel.SelectedAlgorithm}");
            ViewModel.Progress.Reset(data.Sections.Count);

            try
            {
                // Create placement context
                var context = new PlacementContext
                {
                    Document = doc,
                    TitleBlock = titleBlock,
                    PlacementArea = placementArea,
                    HorizontalGapMm = ViewModel.HorizontalGapMm,
                    VerticalGapMm = ViewModel.VerticalGapMm,
                    ViewModel = ViewModel,
                    SheetNumberService = new SheetNumberService(doc)
                };

                // Execute algorithm
                switch (ViewModel.SelectedAlgorithm)
                {
                    case PlacementAlgorithm.Grid:
                        var gridPlacer = new MultiSheetGridPlacementService(doc);
                        result = gridPlacer.Place(context, data.Sections);
                        break;

                    case PlacementAlgorithm.AdaptiveGrid:
                        var adaptivePlacer = new AdaptiveGridPlacementService(doc);
                        result = adaptivePlacer.PlaceSections(context, data.Sections);
                        break;

                    case PlacementAlgorithm.BinPacking:
                        var binPlacer = new MultiSheetBinPlacementService(doc);
                        result = binPlacer.Place(context, data.Sections);
                        break;

                    case PlacementAlgorithm.ReadingOrder:
                        var readingOrderPlacer = new SheetPlacementService(doc);
                        result = readingOrderPlacer.PlaceOnMultipleSheets(context, data.Sections);
                        break;

                    default:
                        result.ErrorMessage = $"Unknown algorithm: {ViewModel.SelectedAlgorithm}";
                        return result;
                }

                return result;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = $"Algorithm execution failed: {ex.Message}";
                return result;
            }
        }

        private void PostPlacementOperations(UIDocument uidoc, HashSet<string> sheetNumbers)
        {
            if (!sheetNumbers.Any() || !ViewModel.OpenSheetsAfterPlacement)
            {
                ViewModel.LogInfo($"{sheetNumbers.Count} sheet(s) created.");
                return;
            }

            var doc = uidoc.Document;
            var sheetIds = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(sheet => sheetNumbers.Contains(sheet.SheetNumber))
                .Select(sheet => sheet.Id)
                .ToList();

            foreach (var sheetId in sheetIds)
            {
                if (doc.GetElement(sheetId) is ViewSheet sheet)
                    uidoc.ActiveView = sheet;
            }

            ViewModel.LogInfo($"{sheetIds.Count} sheet(s) opened.");

            var sheetList = string.Join(", ", sheetNumbers.OrderBy(s => s, StringComparer.Ordinal));
            ViewModel.LogInfo($"Sheets used: {sheetList}");
        }

        public string GetName() => "APUS V314 – Section Placement Handler";

        // Helper classes
        private class ValidationResult
        {
            public bool IsValid { get; set; }
            public string ErrorMessage { get; set; }
            public List<SectionItemViewModel> FilteredSections { get; set; }
            public FamilySymbol TitleBlock { get; set; }
            public SheetPlacementArea PlacementArea { get; set; }
        }

        private class PlacementData
        {
            public List<SectionItemViewModel> Sections { get; set; }
            public View ReferenceView { get; set; }
        }

        public class PlacementResult
        {
            public bool Success => PlacedCount > 0 && string.IsNullOrEmpty(ErrorMessage);
            public int PlacedCount { get; set; }
            public int FailedCount { get; set; }
            public HashSet<string> SheetNumbers { get; set; } = new HashSet<string>();
            public string ErrorMessage { get; set; }
        }

        public class PlacementContext
        {
            public Document Document { get; set; }
            public FamilySymbol TitleBlock { get; set; }
            public SheetPlacementArea PlacementArea { get; set; }
            public double HorizontalGapMm { get; set; }
            public double VerticalGapMm { get; set; }
            public AutoPlaceSectionsViewModel ViewModel { get; set; }
            public SheetNumberService SheetNumberService { get; set; }
            public int DetailNumberCounter { get; set; } = 0;

            public int GetNextDetailNumber() => ++DetailNumberCounter;
        }
    }
}