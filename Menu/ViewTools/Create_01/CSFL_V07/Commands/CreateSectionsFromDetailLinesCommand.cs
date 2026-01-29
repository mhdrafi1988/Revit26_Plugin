using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.CSFL_V07.Filters;
using Revit26_Plugin.CSFL_V07.Views.SectionFromLineDialog;
using Revit26_Plugin.CSFL_V07.ViewModels;
using Revit26_Plugin.CSFL_V07.Services.Orchestration;

namespace Revit26_Plugin.CSFL_V07.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreateSectionsFromDetailLines : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData c,
            ref string message,
            ElementSet elements)
        {
            UIDocument uidoc = c.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            if (doc.ActiveView is not ViewPlan plan)
            {
                TaskDialog.Show("Error", "Run this command from a Plan View.");
                return Result.Failed;
            }

            IList<Reference> refs;
            try
            {
                refs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new StraightDetailLineSelectionFilter(),
                    "Select straight detail lines");
            }
            catch
            {
                return Result.Cancelled;
            }

            if (!refs.Any())
                return Result.Cancelled;

            var dialog = new SectionFromLineDialog(uidoc, c.Application);
            var vm = dialog.ViewModel;

            var orchestrator =
                new SectionFromLineOrchestrator(doc, plan, vm);

            // Hook Create button to execution
            vm.CreateRequested += () =>
            {
                orchestrator.Start(refs);
            };

            dialog.ShowDialog(); // stays open until user cancels

            return Result.Succeeded;
        }
    }
}
