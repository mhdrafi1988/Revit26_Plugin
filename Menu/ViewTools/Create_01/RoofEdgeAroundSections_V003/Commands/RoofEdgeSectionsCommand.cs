using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.RoofEdgeAroundSections.V003
{
    /// <summary>
    /// Entry point: reads the user's pre-launch selection, splits it into
    /// RoofBase elements vs everything else (skipped, per confirmed spec point 2),
    /// and opens the modeless RoofEdgeSectionsWindow.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RoofEdgeAroundSectionsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            ICollection<ElementId> selectedIds = uiDoc.Selection.GetElementIds();

            if (selectedIds.Count == 0)
            {
                TaskDialog.Show("Roof Edge Sections",
                    "Select one or more roofs before launching this tool.");
                return Result.Cancelled;
            }

            var allSelected = selectedIds.Select(id => doc.GetElement(id)).ToList();
            var roofs = allSelected.Where(e => e is RoofBase).ToList();
            var nonRoofs = allSelected.Where(e => e is not RoofBase).ToList();

            if (roofs.Count == 0)
            {
                TaskDialog.Show("Roof Edge Sections",
                    "No roof elements found in the current selection. Select at least one roof and try again.");
                return Result.Cancelled;
            }

            var handler = new RoofEdgeSectionsEventHandler();
            ExternalEvent externalEvent = ExternalEvent.Create(handler);

            var viewModel = new RoofEdgeSectionsViewModel(doc, externalEvent, handler, roofs, nonRoofs);

            var window = new RoofEdgeSectionsWindow(viewModel, uiApp, uiApp.MainWindowHandle);
            window.Show();

            return Result.Succeeded;
        }
    }
}
