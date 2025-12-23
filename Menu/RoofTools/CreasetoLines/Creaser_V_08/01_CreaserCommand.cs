using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.Creaser_V08.Commands.Helpers;
using Revit26_Plugin.Creaser_V08.Commands.UI;
using Revit26_Plugin.Creaser_V08.Commands.ViewModels;

namespace Revit26_Plugin.Creaser_V08.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreaserCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null)
                return Result.Failed;

            Document doc = uiDoc.Document;
            if (doc == null)
                return Result.Failed;

            // --------------------------------------------------
            // 1. SELECTION (NO UI HERE)
            // --------------------------------------------------
            Reference roofRef;
            try
            {
                roofRef = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoofSelectionFilter(),
                    "Select one Roof");
            }
            catch
            {
                return Result.Cancelled;
            }

            Element roof = doc.GetElement(roofRef);
            if (roof == null)
                return Result.Failed;

            // --------------------------------------------------
            // 2. SINGLE WPF WINDOW
            // --------------------------------------------------
            CreaserMainWindow window = new CreaserMainWindow();

            CreaserMainViewModel viewModel =
                new CreaserMainViewModel(
                    doc,
                    roof,
                    doc.ActiveView,
                    () => window.Close());

            window.DataContext = viewModel;

            window.ShowDialog();

            return Result.Succeeded;
        }
    }
}
