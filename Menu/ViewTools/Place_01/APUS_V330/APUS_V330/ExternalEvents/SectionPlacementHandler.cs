// File: ExternalEvents/SectionPlacementHandler.cs
// SINGLE SOURCE OF TRUTH for Revit document modification.
// All document changes occur within a single transaction group.
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V330.Helpers;
using Revit26_Plugin.APUS_V330.Models;
using Revit26_Plugin.APUS_V330.Services;
using Revit26_Plugin.APUS_V330.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V330.ExternalEvents
{
    public class SectionPlacementHandler : IExternalEventHandler
    {
        public AutoPlaceSectionsViewModel  ViewModel       { get; set; }
        public List<SectionItemViewModel>  SectionsToPlace { get; set; }

        public void Execute(UIApplication app)
        {
            var uidoc = app.ActiveUIDocument;
            var doc   = uidoc?.Document;

            if (doc == null)
            {
                ViewModel?.LogError("No active document.");
                ViewModel?.OnPlacementComplete(false, "No active document.");
                return;
            }

            // Stage 1 — validation (no transaction)
            var validation = ValidatePlacement(doc, uidoc);
            if (!validation.IsValid)
            {
                ViewModel?.OnPlacementComplete(false, validation.ErrorMessage);
                return;
            }

            // Stage 2 — prepare data (no transaction)
            var placementData = PreparePlacementData(uidoc, validation.FilteredSections);
            if (placementData == null)
            {
                ViewModel?.OnPlacementComplete(false, "Failed to prepare placement data");
                return;
            }

            // Stage 3 — transaction group (all or nothing)
            using var transactionGroup = new TransactionGroup(doc, "APUS V330 – Grid Section Placement");
            try
            {
                transactionGroup.Start();

                using var transaction = new Transaction(doc, "Place Sections in Grid");
                transaction.Start();

                try
                {
                    var placementResult = ExecutePlacementAlgorithm(
                        doc, placementData, validation.TitleBlock, validation.PlacementArea);

                    if (!placementResult.Success)
                    {
                        transaction.RollBack();
                        transactionGroup.RollBack();
                        ViewModel?.OnPlacementComplete(false, placementResult.ErrorMessage);
                        return;
                    }

                    transaction.Commit();
                    transactionGroup.Assimilate();

                    PostPlacementOperations(uidoc, placementResult.SheetNumbers);
                    ViewModel?.OnPlacementComplete(true,
                        $"{placementResult.PlacedCount} sections placed on {placementResult.SheetNumbers.Count} sheets");
                }
                catch (Exception ex)
                {
                    transaction.RollBack();
                    transactionGroup.RollBack();
                    ViewModel?.LogError($"Placement failed: {ex.Message}");
                    ViewModel?.OnPlacementComplete(false, ex.Message);
                }
            }
            catch (Exception ex)
            {
                ViewModel?.LogError($"Transaction group failed: {ex.Message}");
                ViewModel?.OnPlacementComplete(false, ex.Message);
            }
        }

        // ------------------------------------------------------------------ validation

        private ValidationResult ValidatePlacement(Document doc, UIDocument uidoc)
        {
            var result = new ValidationResult();

            var selected = SectionsToPlace
                ?? ViewModel?.Sections?.Where(x => x.IsSelected).ToList()
                ?? new List<SectionItemViewModel>();

            if (!selected.Any())
            {
                result.ErrorMessage = "No sections selected for placement.";
                return result;
            }

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

            if (!ValidateMargins())
            {
                result.ErrorMessage = "Invalid margin settings.";
                return result;
            }

            var placementArea = CalculatePlacementArea(doc, titleBlock);
            if (placementArea == null)
            {
                result.ErrorMessage = "Failed to calculate placement area.";
                return result;
            }

            if (uidoc.ActiveView == null)
            {
                result.ErrorMessage = "No active view for spatial sorting.";
                return result;
            }

            result.IsValid          = true;
            result.FilteredSections = filtered;
            result.TitleBlock       = titleBlock;
            result.PlacementArea    = placementArea;
            return result;
        }

        private bool ValidateMargins()
        {
            return ViewModel.LeftMarginMm   >= 0 &&
                   ViewModel.RightMarginMm  >= 0 &&
                   ViewModel.TopMarginMm    >= 0 &&
                   ViewModel.BottomMarginMm >= 0;
        }

        private SheetPlacementArea CalculatePlacementArea(Document doc, FamilySymbol titleBlock)
        {
            try
            {
                SheetPlacementArea area = null;
                using var t = new Transaction(doc, "Temporary Sheet Creation");
                t.Start();

                var tempId = CreateTemporarySheet(doc, titleBlock);
                if (tempId == null || tempId == ElementId.InvalidElementId)
                {
                    t.RollBack();
                    return null;
                }

                var tempSheet = doc.GetElement(tempId) as ViewSheet;
                if (tempSheet == null)
                {
                    t.RollBack();
                    return null;
                }

                area = SheetLayoutService.Calculate(
                    tempSheet,
                    ViewModel.LeftMarginMm, ViewModel.RightMarginMm,
                    ViewModel.TopMarginMm,  ViewModel.BottomMarginMm);

                doc.Delete(tempId);
                t.Commit();
                return area;
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
                if (!titleBlock.IsActive) titleBlock.Activate();
                return ViewSheet.Create(doc, titleBlock.Id).Id;
            }
            catch { return null; }
        }

        private PlacementData PreparePlacementData(UIDocument uidoc, List<SectionItemViewModel> sections)
            => new PlacementData { Sections = sections, ReferenceView = uidoc.ActiveView };

        // ------------------------------------------------------------------ placement

        private PlacementResult ExecutePlacementAlgorithm(
            Document doc, PlacementData data,
            FamilySymbol titleBlock, SheetPlacementArea placementArea)
        {
            var result = new PlacementResult();
            ViewModel?.LogInfo("Executing GRID placement algorithm");
            ViewModel.Progress.Reset(data.Sections.Count);

            try
            {
                var context = new PlacementContext
                {
                    Document          = doc,
                    TitleBlock        = titleBlock,
                    PlacementArea     = placementArea,
                    HorizontalGapMm   = ViewModel.HorizontalGapMm,
                    VerticalGapMm     = ViewModel.VerticalGapMm,
                    ViewModel         = ViewModel,
                    SheetNumberService = new SheetNumberService(doc)
                };

                result = new SimpleGridPlacementService(doc).Place(
                    context,
                    data.Sections,
                    data.ReferenceView,
                    ViewModel.GridRows,
                    ViewModel.GridColumns,
                    ViewModel.PlaceToMultipleSheets);

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

            var doc      = uidoc.Document;
            var sheetIds = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .Where(s => sheetNumbers.Contains(s.SheetNumber))
                .Select(s => s.Id)
                .ToList();

            foreach (var id in sheetIds)
                if (doc.GetElement(id) is ViewSheet sheet)
                    uidoc.ActiveView = sheet;

            ViewModel.LogInfo($"{sheetIds.Count} sheet(s) opened.");
            ViewModel.LogInfo($"Sheets: {string.Join(", ", sheetNumbers.OrderBy(s => s, StringComparer.Ordinal))}");
        }

        public string GetName() => "APUS V330 – Grid Section Placement Handler";

        // ------------------------------------------------------------------ inner types

        private class ValidationResult
        {
            public bool                        IsValid          { get; set; }
            public string                      ErrorMessage     { get; set; }
            public List<SectionItemViewModel>  FilteredSections { get; set; }
            public FamilySymbol                TitleBlock       { get; set; }
            public SheetPlacementArea          PlacementArea    { get; set; }
        }

        private class PlacementData
        {
            public List<SectionItemViewModel> Sections      { get; set; }
            public View                       ReferenceView { get; set; }
        }

        public class PlacementResult
        {
            public bool             Success      => PlacedCount > 0 && string.IsNullOrEmpty(ErrorMessage);
            public int              PlacedCount  { get; set; }
            public int              FailedCount  { get; set; }
            public HashSet<string>  SheetNumbers { get; set; } = new();
            public string           ErrorMessage { get; set; }
        }

        public class PlacementContext
        {
            public Document                    Document          { get; set; }
            public FamilySymbol                TitleBlock        { get; set; }
            public SheetPlacementArea          PlacementArea     { get; set; }
            public double                      HorizontalGapMm   { get; set; }
            public double                      VerticalGapMm     { get; set; }
            public AutoPlaceSectionsViewModel  ViewModel         { get; set; }
            public SheetNumberService          SheetNumberService { get; set; }
            public int                         DetailNumberCounter { get; set; } = 0;

            public int GetNextDetailNumber() => ++DetailNumberCounter;
        }
    }
}
