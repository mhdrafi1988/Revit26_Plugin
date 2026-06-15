using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;

namespace Revit_26.CornertoDrainArrow_V05
{
    [Transaction(TransactionMode.Manual)]
    public class RoofDrainageCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            if (uidoc == null)
            {
                message = "No active document.";
                return Result.Failed;
            }

            ElementId selectedRoofId;

            try
            {
                // 🔒 Revit-safe selection BEFORE UI
                Reference pickedRef = uidoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoofSelectionFilter(),
                    "Pick a roof to analyze drainage");

                selectedRoofId = pickedRef.ElementId;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User pressed ESC → silent cancel
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }

            // ✅ Valid roof selected → now UI is allowed
            var logService = new LogService();
            var viewModel = new RoofDrainageViewModel(logService);

            var window = new RoofDrainageWindow
            {
                DataContext = viewModel
            };

            window.ShowDialog();
            return Result.Succeeded;
        }
    }
}
