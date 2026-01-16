using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V306.Helpers;
using Revit26_Plugin.APUS_V306.Models;
using Revit26_Plugin.APUS_V306.Services;
using Revit26_Plugin.APUS_V306.ViewModels;
using System.Linq;

namespace Revit26_Plugin.APUS_V306.ExternalEvents
{
    /// <summary>
    /// ExternalEvent handler for Auto Place Sections (APUS).
    /// Owns:
    /// - Revit API execution
    /// - Sorting order
    /// - Sheet creation loop
    /// - Global detail numbering
    /// </summary>
    public class AutoPlaceSectionsHandler : IExternalEventHandler
    {
        public AutoPlaceSectionsViewModel ViewModel { get; set; }

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc.Document;

            if (uidoc == null || doc == null)
            {
                ViewModel.LogError("No active document.");
                return;
            }

            // --------------------------------------------------
            // 1?? Collect selected sections
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
            // 2?? Apply UI filters
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
            // 3?? Sort by projected X/Y in reference view (Option A)
            // --------------------------------------------------
            View referenceView = uidoc.ActiveView;

            if (referenceView == null)
            {
                ViewModel.LogError("No active view available for sorting.");
                return;
            }

            var sorted = SectionSortingService.Sort(
                filtered,
                referenceView);

            // Optional debug – placement order
            foreach (var s in sorted)
            {
                ViewModel.LogInfo($"ORDER ? {s.Name}");
            }

            // --------------------------------------------------
            // 4?? Initialize progress + transaction
            // --------------------------------------------------
            ViewModel.Progress.Reset(sorted.Count);

            using Transaction tx =
                new Transaction(doc, "APUS – Auto Place Sections");

            tx.Start();

            // --------------------------------------------------
            // 5?? Resolve title block
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

            var sheetCreator = new SheetCreationService(doc);
            var placer = new SheetPlacementService(doc);

            int sheetIndex = 1;
            int cursor = 0;

            // ? Global continuous detail number
            int globalDetailIndex = 0;

            // --------------------------------------------------
            // 6?? Multi-sheet placement loop
            // --------------------------------------------------
            while (cursor < sorted.Count)
            {
                if (ViewModel.Progress.IsCancelled)
                {
                    ViewModel.LogWarning("Placement cancelled by user.");
                    break;
                }

                ViewSheet sheet =
                    sheetCreator.Create(titleBlock, sheetIndex++);

                SheetPlacementArea area =
                    SheetLayoutService.Calculate(
                        sheet,
                        ViewModel.LeftMarginMm,
                        ViewModel.RightMarginMm,
                        ViewModel.TopMarginMm,
                        ViewModel.BottomMarginMm);

                double gapFt =
                    UnitConversionHelper.MmToFeet(
                        ViewModel.HorizontalGapMm);

                int placed = placer.PlaceBatch(
                    sheet,
                    sorted.Skip(cursor).ToList(),
                    area,
                    gapFt,
                    ViewModel,
                    ref globalDetailIndex);

                if (placed == 0)
                {
                    ViewModel.LogWarning(
                        $"No more views fit on sheet {sheet.SheetNumber}.");
                    break;
                }

                cursor += placed;
            }

            tx.Commit();
            ViewModel.LogInfo("Placement complete.");
        }

        public string GetName()
        {
            return "APUS – Auto Place Sections Handler";
        }
    }
}
