using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Revit26_Plugin.WSA_V05.Views;
using System;

namespace Revit26_Plugin.WSA_V05.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateWorksetsFromLinkedFilesV05 : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            Autodesk.Revit.DB.ElementSet elements)
        {
            // Senior Tip: Always validate the UI context first
            if (commandData?.Application?.ActiveUIDocument?.Document == null)
            {
                message = "Active document not found.";
                return Result.Cancelled;
            }

            try
            {
                var window = new WorksetSelectorWindow(commandData);
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