using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Linq;
using Revit26_Plugin.RRLPV4.ViewModels;
using Revit26_Plugin.RRLPV4.Views;
using Revit26_Plugin.RRLPV4.Utils;

namespace Revit26_Plugin.RRLPV4.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class RoofRidgeCommand_04 : IExternalCommand
    {
        public Result Execute(ExternalCommandData c, ref string message, ElementSet elements)
        {
            try
            {
                UIDocument uidoc = c.Application.ActiveUIDocument;
                if (uidoc?.Document == null)
                {
                    TaskDialog.Show("Error", "No active document.");
                    return Result.Failed;
                }

                Document doc = uidoc.Document;

                var roofs = new FilteredElementCollector(doc, uidoc.Selection.GetElementIds())
                    .OfCategory(BuiltInCategory.OST_Roofs)
                    .WhereElementIsNotElementType()
                    .Cast<RoofBase>()
                    .ToList();

                if (roofs.Count != 1)
                {
                    TaskDialog.Show("Selection", "Please select exactly one roof.");
                    return Result.Cancelled;
                }

                Logger.LogInfo($"Command started with roof ID: {roofs.First().Id}");

                var vm = new MainViewModel(roofs.First(), uidoc);
                var window = new MainWindow { DataContext = vm };
                window.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "Command execution");
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}