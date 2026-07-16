using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using System;
using Revit26_Plugin.RoomToRoofUI;

namespace Revit26_Plugin.RoomToRoof
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    public class RoomToRoofCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;

                RoomToRoofWindow ui = new RoomToRoofWindow(uiApp);
                ui.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}