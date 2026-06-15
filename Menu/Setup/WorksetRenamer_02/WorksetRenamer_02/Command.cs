using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.WorksetRenamer_01.ViewModels;
using Revit26_Plugin.WorksetRenamer_01.Views;

namespace Revit26_Plugin.WorksetRenamer_01
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
                    TaskDialog.Show("Workset Renamer",
                        "This tool requires a workshared model.\n" +
                        "Enable worksharing before running Workset Renamer.");
                    return Result.Cancelled;
                }

                var viewModel = new WorksetRenamerViewModel(doc);
                var view      = new WorksetRenamerView(viewModel);
                view.ShowDialog();

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
