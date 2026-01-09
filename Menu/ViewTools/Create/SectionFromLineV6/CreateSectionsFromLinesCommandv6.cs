using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit22_Plugin.PlanSections.Views;
using Revit22_Plugin.PlanSections.ViewModels;
using Revit22_Plugin.PlanSections.Filters;
using Revit22_Plugin.PlanSections.Services;


namespace Revit22_Plugin.PlanSections
{
    [Transaction(TransactionMode.Manual)]
    public class CreateSectionsFromLinesCommandv6 : IExternalCommand
    {
        public Result Execute(ExternalCommandData c, ref string message, ElementSet elements)
        {
            UIDocument uidoc = c.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            // Check that we are in a plan view
            if (!(doc.ActiveView is ViewPlan plan))
            {
                TaskDialog.Show("Error", "Run this command only from a Plan View.");
                return Result.Failed;
            }

            IList<Reference> pickedRefs;

            try
            {
                // user must pick straight detail lines
                pickedRefs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new DetailLineSelectionFilter(),
                    "Select straight Detail Lines"
                );

                if (pickedRefs == null || !pickedRefs.Any())
                    return Result.Cancelled;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            // Show dialog
            var dialog = new SectionDialogDtlLineWindow(uidoc, c.Application);

            if (dialog.ShowDialog() != true)
                return Result.Cancelled;

            var vm = dialog.DataContext as SectionDialogDtlLineViewModel;
            if (vm == null)
                return Result.Failed;

            // Create the v6 section creation service
            var service = new SectionFromLineCreationService_v6(doc, plan, vm);

            // Execute section creation
            service.CreateSectionsFromLines(pickedRefs);

            return Result.Succeeded;
        }
    }
}
