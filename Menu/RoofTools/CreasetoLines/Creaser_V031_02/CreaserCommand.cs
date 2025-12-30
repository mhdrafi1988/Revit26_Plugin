using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.Creaser_V31.Views;

namespace Revit26_Plugin.Creaser_V31.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreaserCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            // Validate Revit context early (never let bad state leak)
            if (!Helpers.RevitContextGuard.IsValidPlanView(
                    commandData, out UIDocument uiDoc, out Document doc))
            {
                message = "Command works only in plan views with an active document.";
                return Result.Failed;
            }

            // WPF UI only – NO Revit logic here
            CreaserView view = new CreaserView(uiDoc);
            view.ShowDialog();

            return Result.Succeeded;
        }
    }
}
