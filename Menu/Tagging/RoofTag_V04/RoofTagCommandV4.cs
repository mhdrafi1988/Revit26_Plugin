using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.RoofTagV4.Helpers;
using Revit22_Plugin.RoofTagV4.Models;
using Revit22_Plugin.RoofTagV4.Services;
using Revit22_Plugin.RoofTagV4.Views;

namespace Revit22_Plugin.RoofTagV4
{
    [Transaction(TransactionMode.Manual)]
    public class RoofTagCommandV4 : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData,
                              ref string message,
                              ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;

            // ------------------------------------------
            // 1️⃣ PICK ROOF BEFORE UI
            // ------------------------------------------
            RoofBase roof = SelectionHelperV4.SelectRoof(uiDoc);

            if (roof == null)
            {
                TaskDialog.Show("Roof Tag V4", "Please select a roof.");
                return Result.Cancelled;
            }

            // ------------------------------------------
            // 2️⃣ Extract geometry BEFORE UI opens
            // ------------------------------------------
            RoofLoopsModel geom = RoofGeometryServiceV4.BuildRoofGeometry(roof);

            // ------------------------------------------
            // 3️⃣ Open UI with roof + geometry
            // ------------------------------------------
            var win = new RoofTagWindowV4(uiApp, roof, geom);
            win.ShowDialog();

            return Result.Succeeded;
        }
    }
}
