// =======================================================
// File: CreaserAdvCommand.cs
// Namespace: Revit26_Plugin.CreaserAdv.V010.Commands
// Changes vs V009:
//   FIX  Window shown modeless (.Show()) instead of modal (.ShowDialog()),
//        parented to Revit's main window — the convention the other roof
//        tools already follow. Run goes through an ExternalEvent
//        (CreaserAdvHandler), and Revit only services an ExternalEvent when
//        it is idle; while a modal dialog holds this Execute() open the
//        event is not reliably serviced, so Run could sit queued until the
//        window was closed (the sibling tools made the same fix).
//   NEW  Logs the log-file path and the picked roof's name/id.
// =======================================================

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.CreaserAdv.V010.Services;
using Revit26_Plugin.CreaserAdv.V010.ViewModels;
using Revit26_Plugin.CreaserAdv.V010.Views;
using System;
using System.Windows.Interop;
using Revit26_Plugin.Utilities;

namespace Revit26_Plugin.CreaserAdv.V010.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreaserAdvCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string          message,
            ElementSet          elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument    uiDoc = uiApp.ActiveUIDocument;

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
                logger.Info("Creaser Advanced V010 started.");
                logger.Info($"Log file: {logger.LogFilePath}");

                Element roof;
                try
                {
                    roof = new RoofSelectionService().SelectSingleRoof(uiDoc);
                    logger.Info($"Roof selected: {roof.Name} (id {roof.Id.Value})");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    logger.Warning("Roof selection cancelled by user.");
                    return Result.Cancelled;
                }

                var viewModel = new CreaserAdvViewModel(uiApp, roof, logger);
                var window    = new CreaserAdvWindow(viewModel);

                new WindowInteropHelper(window).Owner = uiApp.MainWindowHandle;
                window.Show(); // modeless — Run is serviced by the ExternalEvent

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Error("CreaserAdvCommand", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
