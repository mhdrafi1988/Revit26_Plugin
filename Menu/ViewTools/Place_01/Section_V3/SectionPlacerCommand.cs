using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;

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

            List<ViewSection> sectionViews = SectionUtils.CollectUnplacedSectionsInPlan(doc, activeView);

            if (sectionViews.Count == 0)
            {
                TaskDialogResult result = TaskDialog.Show(
                    "No Sections Found",
                    "No unplaced sections found in this view.\nDo you want to check the entire project?",
                    TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.No);

                if (result == TaskDialogResult.Yes)
                {
                    sectionViews = SectionUtils.CollectUnplacedSectionsInProject(doc);
                }
                else
                {
                    return Result.Cancelled;
                }
            }

            if (sectionViews.Count == 0)
            {
                TaskDialog.Show("Info", "No unplaced section views found.");
                return Result.Cancelled;
            }

            SectionPlacementWindow window = new SectionPlacementWindow();
            SectionPlacementViewModel vm = new SectionPlacementViewModel(doc, sectionViews);
            window.DataContext = vm;

            bool? dialogResult = window.ShowDialog();
            if (dialogResult != true) return Result.Cancelled;

            using (Transaction t = new Transaction(doc, "Place Sections on Sheets"))
            {
                t.Start();
                SectionUtils.PlaceSectionsOnSheets(doc, sectionViews, vm);
                t.Commit();
            }

            TaskDialog.Show("Success", $"Placed {sectionViews.Count} sections on sheets.");
            return Result.Succeeded;
        }
    }
}
