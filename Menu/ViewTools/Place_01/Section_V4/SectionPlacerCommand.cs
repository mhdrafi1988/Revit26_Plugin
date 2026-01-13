using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.SectionPlacement
{
    [Transaction(TransactionMode.Manual)]
    public class SectionPlacerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = doc.ActiveView;

            if (!(activeView is ViewPlan))
            {
                TaskDialog.Show("Error", "Please make a Plan View the active view and run again.");
                return Result.Cancelled;
            }

            // 🔄 Now collect ALL sections in active plan (placed + unplaced)
            List<ViewSection> sectionViews = SectionUtils.CollectAllSectionsInPlan(doc, activeView);

            if (sectionViews.Count == 0)
            {
                TaskDialog.Show("Info", "No sections found in this plan view.");
                return Result.Cancelled;
            }

            // Open placement window
            SectionPlacementWindow window = new SectionPlacementWindow();
            SectionPlacementViewModel vm = new SectionPlacementViewModel(doc, sectionViews);
            window.DataContext = vm;

            bool? dialogResult = window.ShowDialog();
            if (dialogResult != true) return Result.Cancelled;

            // Get only selected sections from the grid
            var selectedSections = window.SectionsGrid.SelectedItems
                .Cast<SectionItem>()
                .Select(si => si.Section)
                .ToList();

            if (selectedSections.Count == 0)
            {
                TaskDialog.Show("Info", "No sections selected.");
                return Result.Cancelled;
            }

            using (Transaction t = new Transaction(doc, "Move Sections to Sheets"))
            {
                t.Start();
                SectionUtils.MoveSectionsToSheets(doc, selectedSections, vm);
                t.Commit();
            }

            return Result.Succeeded;
        }
    }
}
