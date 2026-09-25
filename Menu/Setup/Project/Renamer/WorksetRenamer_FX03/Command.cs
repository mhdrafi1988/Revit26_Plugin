using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.WorksetRenamer.FX03.ViewModels;
using Revit26_Plugin.WorksetRenamer.FX03.Views;
using Revit26_Plugin.Utilities;

namespace Revit26_Plugin.WorksetRenamer.FX03
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Document doc = commandData.Application.ActiveUIDocument.Document;

                // Guard: worksharing must be enabled
                if (!doc.IsWorkshared)
                {
                    TaskDialog.Show("Workset Renamer — From Excel",
                        "This tool requires a workshared model.\n" +
                        "Enable worksharing before running Workset Renamer FX03.");
                    return Result.Cancelled;
                }

                var viewModel = new WorksetRenamerFxViewModel(doc);
                var view = new WorksetRenamerFxView(viewModel);
                new WindowInteropHelper(view).Owner = commandData.Application.MainWindowHandle;
                view.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.Error("Command", ex);
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
