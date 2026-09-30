using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.ScheduleExportImport.V001.Infrastructure.ExternalEvents;
using Revit26_Plugin.ScheduleExportImport.V001.UI.ViewModels;
using Revit26_Plugin.ScheduleExportImport.V001.UI.Views;

namespace Revit26_Plugin.ScheduleExportImport.V001.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ScheduleExportImportCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;
                var uiDoc = uiApp.ActiveUIDocument;
                var doc = uiDoc.Document;

                var handler = new ScheduleImportEventHandler(doc);
                var externalEvent = ExternalEvent.Create(handler);

                var viewModel = new ScheduleExportImportViewModel(doc, handler, externalEvent);
                var view = new ScheduleExportImportWindow(viewModel);
                new WindowInteropHelper(view).Owner = uiApp.MainWindowHandle;
                view.Show();

                return Result.Succeeded;
            }
            catch (OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Schedule Export/Import", $"Error opening tool:\n{ex.Message}");
                return Result.Failed;
            }
        }
    }
}
