using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.CreaserAdv_V00.Services.Logging;
using Revit26_Plugin.CreaserAdv_V00.ViewModels;
using Revit26_Plugin.CreaserAdv_V00.Views;
using System;

namespace Revit26_Plugin.CreaserAdv_V00.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreaserAdvCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uiDoc = uiApp.ActiveUIDocument;

                if (uiDoc?.Document == null)
                    return Result.Cancelled;

                if (uiDoc.ActiveView is not ViewPlan)
                {
                    TaskDialog.Show("Creaser Advanced", "Run from a Plan View.");
                    return Result.Cancelled;
                }

                var log = new LoggingService("CreaserAdv");

                Element roof;
                try
                {
                    roof =
                        new Revit26_Plugin.CreaserAdv_V00.Services.Selection.RoofSelectionService()
                            .SelectSingleRoof(uiDoc);
                }
                catch
                {
                    log.Warning("Roof selection cancelled.");
                    return Result.Cancelled;
                }

                var vm = new CreaserAdvViewModel(uiApp, roof, log);
                var window = new CreaserAdvWindow(vm);
                window.ShowDialog();

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
