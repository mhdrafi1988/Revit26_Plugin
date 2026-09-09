using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.RoofEdgeElementSections.V001
{
    /// <summary>
    /// Entry point: no pre-launch selection is required (per confirmed spec) —
    /// the modeless window itself has a "Pick Roofs" button. Creates the
    /// ExternalEvent/handler, constructs the ViewModel, and opens the window.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RoofEdgeElementSectionsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;

            var handler = new RoofEdgeElementSectionsEventHandler();
            ExternalEvent externalEvent = ExternalEvent.Create(handler);

            var viewModel = new RoofEdgeElementSectionsViewModel(doc, uiDoc, externalEvent, handler);

            var window = new RoofEdgeElementSectionsWindow(viewModel, uiApp, uiApp.MainWindowHandle);
            window.Show();

            return Result.Succeeded;
        }
    }
}
