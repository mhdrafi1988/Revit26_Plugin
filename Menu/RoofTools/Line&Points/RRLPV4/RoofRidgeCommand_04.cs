using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Linq;
using Revit26_Plugin.RRLPV4.ViewModels;
using Revit26_Plugin.RRLPV4.Views;

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
                    .Cast<Autodesk.Revit.DB.RoofBase>()
                    .ToList();

                if (!roofs.Any())
                {
                    TaskDialog.Show("Selection", "Select exactly one roof.");
                    return Result.Cancelled;
                }

                var vm = new MainViewModel(roofs.First());
                var window = new MainWindow { DataContext = vm };
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
