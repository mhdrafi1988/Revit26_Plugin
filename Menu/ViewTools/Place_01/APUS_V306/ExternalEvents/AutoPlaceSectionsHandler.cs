using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V306.Helpers;
using Revit26_Plugin.APUS_V306.Models;
using Revit26_Plugin.APUS_V306.Services;
using Revit26_Plugin.APUS_V306.ViewModels;
using System.Linq;

namespace Revit26_Plugin.APUS_V306.ExternalEvents
{
    public class AutoPlaceSectionsHandler : IExternalEventHandler
    {
        public AutoPlaceSectionsViewModel ViewModel { get; set; }

        public void Execute(UIApplication app)
        {
            UIDocument uidoc = app.ActiveUIDocument;
            Document doc = uidoc?.Document;

            if (doc == null)
            {
                ViewModel.LogError("No active document.");
                return;
            }

            var selected =
                ViewModel.Sections.Where(x => x.IsSelected).ToList();

            if (!selected.Any())
            {
                ViewModel.LogWarning("No sections selected.");
                return;
            }

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

            View referenceView = uidoc.ActiveView;
            if (referenceView == null)
            {
                ViewModel.LogError("No active view for sorting.");
                return;
            }

            var sortedWithCoords =
                SectionSortingService.SortLeftToRight(
                    filtered,
                    referenceView);

            int order = 1;
            foreach (var s in sortedWithCoords)
            {
                ViewModel.LogInfo(
                    $"SORT {order++:00} ? {s.Item.Name} (X={s.X:0.0}, Y={s.Y:0.0})");
            }

            var sorted =
                sortedWithCoords.Select(x => x.Item).ToList();

            ViewModel.Progress.Reset(sorted.Count);

            using Transaction tx =
                new Transaction(doc, "APUS – Auto Place Sections");

            tx.Start();

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
            int globalDetailIndex = 0;

            while (cursor < sorted.Count)
            {
                if (ViewModel.Progress.IsCancelled)
                {
                    ViewModel.LogWarning("Placement cancelled.");
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

                double gapMm = ViewModel.HorizontalGapMm;

                int placed = placer.PlaceBatchOrdered(
                        sheet,
                        sorted,                       // already sorted list
                        area,
                        ViewModel.HorizontalGapMm,    // mm
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
