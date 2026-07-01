// ==================================
// File: CreaserAdvCommand.cs
// Namespace: Revit26_Plugin.CreaserAdv_V003_01
// ==================================

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.CreaserAdv_V003_01.Services;
using Revit26_Plugin.CreaserAdv_V003_01.ViewModels;
using Revit26_Plugin.CreaserAdv_V003_01.Views;
using System;

namespace Revit26_Plugin.CreaserAdv_V003_01.Commands
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
                {
                    TaskDialog.Show("Creaser Advanced", "No active document found.");
                    return Result.Cancelled;
                }

                if (uiDoc.ActiveView is not ViewPlan)
                {
                    TaskDialog.Show("Creaser Advanced", "Please run this command from a Plan View.");
                    return Result.Cancelled;
                }

                var logger = new LoggingService("CreaserAdv");
                logger.Info("Creaser Advanced command started.");

                Element roof;
                try
                {
                    roof = new RoofSelectionService().SelectSingleRoof(uiDoc);
                    logger.Info($"Roof selected: {roof.Id}");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    logger.Warning("Roof selection canceled by user.");
                    return Result.Cancelled;
                }

                var viewModel = new CreaserAdvViewModel(uiApp, roof, logger);
                var window = new CreaserAdvWindow(viewModel);
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
