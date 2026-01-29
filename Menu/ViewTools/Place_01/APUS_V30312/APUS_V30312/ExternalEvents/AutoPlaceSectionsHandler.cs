using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V312.Models;
using Revit26_Plugin.APUS_V312.Services;
using Revit26_Plugin.APUS_V312.ViewModels;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Revit26_Plugin.APUS_V312.ExternalEvents
{
    /// <summary>
    /// GRID-based auto placement of section views.
    /// This handler intentionally mirrors the proven V311 logic flow:
    /// Selection ? Filter ? Sort ? Multi-sheet Grid Placement.
    /// 
    /// All UI values are read safely via reflection to avoid
    /// hard dependencies on ViewModel properties.
    /// </summary>
    public class AutoPlaceSectionsHandler : IExternalEventHandler
    {
        /// <summary>
        /// ViewModel injected by AutoPlaceSectionsEventManager.
        /// </summary>
        public AutoPlaceSectionsViewModel ViewModel { get; set; }

        /// <summary>
        /// Optional pre-filtered list passed by the ExternalEvent.
        /// If null, handler falls back to ViewModel.Sections.
        /// </summary>
        public List<SectionItemViewModel> SectionsToPlace { get; set; }

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc?.Document;

            if (doc == null)
            {
                ViewModel?.LogError("No active document.");
                return;
            }

            // --------------------------------------------------
            // 1. Collect selected sections (STABLE INPUT)
            // --------------------------------------------------
            var selected =
                (SectionsToPlace ?? ViewModel.Sections.Where(x => x.IsSelected))
                    .ToList();

            if (!selected.Any())
            {
                ViewModel.LogWarning("No sections selected.");
                return;
            }

            // --------------------------------------------------
            // 2. Apply UI filters (ONCE)
            // --------------------------------------------------
            var filtered =
                SectionFilterService.Apply(
                    selected,
                    ViewModel.SelectedPlacementScope,
                    ViewModel.SelectedPlacementState,
                    ViewModel.SheetNumberFilter);

            if (!filtered.Any())
            {
                ViewModel.LogWarning("No sections passed filtering.");
                return;
            }

            // --------------------------------------------------
            // 3. Spatial sorting (Y tolerance via reflection)
            // --------------------------------------------------
            View referenceView = uidoc.ActiveView;
            if (referenceView == null)
            {
                ViewModel.LogError("No active view for spatial sorting.");
                return;
            }

            double yToleranceMm =
                ReadDouble(ViewModel, "YToleranceMm", "SortToleranceMm");

            var sorted =
                SectionSortingService
                    .SortLeftToRight(
                        filtered,
                        referenceView,
                        yToleranceMm)
                    .Select(x => x.Item)
                    .ToList();

            // --------------------------------------------------
            // 4. Initialize progress with FINAL list
            // --------------------------------------------------
            ViewModel.Progress.Reset(sorted.Count);

            // ==================================================
            // TRANSACTION — MODEL MODIFICATIONS ONLY
            // ==================================================
            using Transaction tx =
                new Transaction(doc, "APUS – Auto Place Sections (Grid)");

            tx.Start();

            try
            {
                // --------------------------------------------------
                // Resolve selected title block
                // --------------------------------------------------
                if (ViewModel.SelectedTitleBlock == null)
                {
                    ViewModel.LogError("No title block selected.");
                    tx.RollBack();
                    return;
                }

                FamilySymbol titleBlock =
                    doc.GetElement(ViewModel.SelectedTitleBlock.SymbolId)
                        as FamilySymbol;

                if (titleBlock == null)
                {
                    ViewModel.LogError("Selected title block is invalid.");
                    tx.RollBack();
                    return;
                }

                // --------------------------------------------------
                // Read sheet margins (mm) via reflection
                // --------------------------------------------------
                double leftMm = ReadDouble(ViewModel, "LeftMarginMm", "OffsetLeftMm");
                double rightMm = ReadDouble(ViewModel, "RightMarginMm", "OffsetRightMm");
                double topMm = ReadDouble(ViewModel, "TopMarginMm", "OffsetTopMm");
                double bottomMm = ReadDouble(ViewModel, "BottomMarginMm", "OffsetBottomMm");

                // --------------------------------------------------
                // Calculate placement area ONCE
                // --------------------------------------------------
                var sheetCreator = new SheetCreationService(doc);
                ViewSheet tempSheet = sheetCreator.Create(titleBlock, 0);

                SheetPlacementArea placementArea =
                    SheetLayoutService.Calculate(
                        tempSheet,
                        leftMm,
                        rightMm,
                        topMm,
                        bottomMm);

                // Remove temp sheet immediately
                doc.Delete(tempSheet.Id);

                // --------------------------------------------------
                // Read grid gaps (mm) via reflection
                // --------------------------------------------------
                double horizontalGapMm =
                    ReadDouble(ViewModel, "HorizontalGapMm", "GapHorizontalMm");

                double verticalGapMm =
                    ReadDouble(ViewModel, "VerticalGapMm", "GapVerticalMm");

                // --------------------------------------------------
                // GRID-BASED MULTI-SHEET PLACEMENT
                // --------------------------------------------------
                var placer =
                    new MultiSheetGridPlacementService(doc);

                placer.Place(
                    sorted,
                    titleBlock,
                    placementArea,
                    horizontalGapMm,
                    verticalGapMm,
                    ViewModel);

                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.RollBack();
                ViewModel.LogError($"Placement failed: {ex.Message}");
                return;
            }

            // ==================================================
            // UI ACTIONS — NO TRANSACTION
            // ==================================================

            // Open all sheets that received placed views
            List<ElementId> sheetIds =
                new FilteredElementCollector(doc)
                    .OfClass(typeof(Viewport))
                    .Cast<Viewport>()
                    .Where(vp => sorted.Any(s => s.View.Id == vp.ViewId))
                    .Select(vp => vp.SheetId)
                    .Distinct()
                    .ToList();

            foreach (ElementId sheetId in sheetIds)
            {
                if (doc.GetElement(sheetId) is ViewSheet sheet)
                    uidoc.ActiveView = sheet;
            }

            ViewModel.LogInfo(
                $"Grid placement complete. {sheetIds.Count} sheet(s) opened.");
        }

        public string GetName()
        {
            return "APUS – Auto Place Sections Handler (Grid)";
        }

        // --------------------------------------------------
        // Reflection helper (safe, reusable)
        // --------------------------------------------------
        private static double ReadDouble(object obj, params string[] propertyNames)
        {
            if (obj == null)
                return 0;

            Type t = obj.GetType();

            foreach (string name in propertyNames)
            {
                PropertyInfo pi =
                    t.GetProperty(name, BindingFlags.Instance | BindingFlags.Public);

                if (pi == null)
                    continue;

                object value = pi.GetValue(obj);
                if (value == null)
                    return 0;

                if (value is double d)
                    return d;

                if (value is int i)
                    return i;

                if (double.TryParse(
                        value.ToString(),
                        NumberStyles.Any,
                        CultureInfo.InvariantCulture,
                        out double parsed))
                    return parsed;

                return 0;
            }

            return 0;
        }
    }
}
