using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;
using Revit26_Plugin.CreateSectionsFromDetailLines.V008.Filters;
using Revit26_Plugin.CreateSectionsFromDetailLines.V008.Views.SectionFromLineDialog;
using Revit26_Plugin.CreateSectionsFromDetailLines.V008.Services;

namespace Revit26_Plugin.CreateSectionsFromDetailLines.V008.Commands
{
    /// <summary>
    /// V008 changes from V07:
    /// - Dialog is shown modeless (Show()) instead of modal (ShowDialog()),
    ///   per suite convention. WindowInteropHelper sets the owner handle
    ///   from commandData.Application.MainWindowHandle (never
    ///   Application.Current.MainWindow, which is unreliable in Revit's
    ///   host process).
    /// - "Create" now raises a real, cached ExternalEvent
    ///   (SectionCreationExternalEvent) instead of calling the orchestrator
    ///   directly on the UI thread.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class CreateSectionsFromDetailLines : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            if (doc.ActiveView is not ViewPlan plan)
            {
                TaskDialog.Show("Create Sections From Detail Lines", "Run this command from a Plan View.");
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

            var dialog = new SectionFromLineDialog(uidoc, commandData.Application);
            var vm = dialog.ViewModel;

            // Modeless owner wiring — required so the dialog stays above
            // Revit's main window without blocking it.
            new WindowInteropHelper(dialog).Owner = commandData.Application.MainWindowHandle;

            var externalEvent = new SectionCreationExternalEvent(uidoc, plan, vm);

            vm.CreateRequested += () =>
            {
                externalEvent.Raise(refs);
            };

            dialog.Show(); // modeless — stays open, Revit remains usable

            return Result.Succeeded;
        }
    }
}
