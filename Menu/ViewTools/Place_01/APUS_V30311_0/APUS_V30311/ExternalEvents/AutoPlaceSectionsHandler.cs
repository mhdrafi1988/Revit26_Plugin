using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V311.Models;
using Revit26_Plugin.APUS_V311.Services;
using Revit26_Plugin.APUS_V311.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V311.ExternalEvents
{
    /// <summary>
    /// Executes grid-based auto placement of section views.
    /// This class MUST keep this exact name and namespace
    /// because AutoPlaceSectionsEventManager depends on it.
    /// </summary>
    public class AutoPlaceSectionsHandler : IExternalEventHandler
    {
        public AutoPlaceSectionsViewModel ViewModel { get; set; }

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
            // Collect selected sections
            // --------------------------------------------------
            var selected = ViewModel.Sections
                .Where(x => x.IsSelected)
                .ToList();

            if (!selected.Any())
            {
                ViewModel.LogWarning("No sections selected.");
                return;
            }

            // --------------------------------------------------
            // Apply filters
            // --------------------------------------------------
            var filtered = SectionFilterService.Apply(
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
            // Sort sections spatially
            // --------------------------------------------------
            View referenceView = uidoc.ActiveView;
            if (referenceView == null)
            {
                ViewModel.LogError("No active view.");
                return;
            }

            var sorted = SectionSortingService
                .SortLeftToRight(
                    filtered,
                    referenceView,
                    ViewModel.YToleranceMm)
                .Select(x => x.Item)
                .ToList();

            ViewModel.Progress.Reset(sorted.Count);

            // ==================================================
            // TRANSACTION — PLACE SECTIONS
            // ==================================================
            using (Transaction tx =
                new Transaction(doc, "APUS – Auto Place Sections (Grid)"))
            {
                tx.Start();

                // --------------------------------------------------
                // Resolve title block
                // --------------------------------------------------
                var titleBlock =
                    new FilteredElementCollector(doc)
                        .OfClass(typeof(FamilySymbol))
                        .Cast<FamilySymbol>()
                        .FirstOrDefault(x =>
                            x.Category.Id.Value ==
                            (int)BuiltInCategory.OST_TitleBlocks);

                if (titleBlock == null)
                {
                    ViewModel.LogError("No title block found.");
                    tx.RollBack();
                    return;
                }

                // --------------------------------------------------
                // Calculate placement area ONCE
                // --------------------------------------------------
                var sheetCreator = new SheetCreationService(doc);
                ViewSheet tempSheet = sheetCreator.Create(titleBlock, 0);

                SheetPlacementArea placementArea =
                    SheetLayoutService.Calculate(
                        tempSheet,
                        ViewModel.LeftMarginMm,
                        ViewModel.RightMarginMm,
                        ViewModel.TopMarginMm,
                        ViewModel.BottomMarginMm);

                // Remove temp sheet
                doc.Delete(tempSheet.Id);

                // --------------------------------------------------
                // GRID-BASED MULTI-SHEET PLACEMENT
                // --------------------------------------------------
                var placer = new MultiSheetGridPlacementService(doc);

                placer.Place(
                    sorted,
                    titleBlock,
                    placementArea,
                    ViewModel.HorizontalGapMm,
                    ViewModel.VerticalGapMm,
                    ViewModel);

                tx.Commit();
            }

            // ==================================================
            // OPEN SHEETS USED IN PLACEMENT (NO TRANSACTION)
            // ==================================================

            List<ElementId> sheetIds = new FilteredElementCollector(doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .Where(vp => sorted.Any(s => s.View.Id == vp.ViewId))
                .Select(vp => vp.SheetId)
                .Distinct()
                .ToList();

            foreach (ElementId sheetId in sheetIds)
            {
                if (doc.GetElement(sheetId) is ViewSheet sheet)
                {
                    uidoc.ActiveView = sheet; // UI operation – no transaction
                }
            }

            // --------------------------------------------------
            // Refresh UI AFTER all model + UI actions
            // --------------------------------------------------
            ViewModel?.RequestUiRefresh();

            ViewModel.LogInfo(
                $"Grid placement complete. {sheetIds.Count} sheet(s) opened.");
        }

        public string GetName()
        {
            return "APUS – Auto Place Sections Handler (Grid)";
        }
    }
}
