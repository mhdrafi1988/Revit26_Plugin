using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.RoofPointComparison.V001.Infrastructure.Helpers;
using Revit26_Plugin.RoofPointComparison.V001.UI.Views;

namespace Revit26_Plugin.RoofPointComparison.V001.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class RoofComparisonCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData data, ref string msg, ElementSet elems)
        {
            UIDocument uidoc = data.Application.ActiveUIDocument;
            Document doc = uidoc.Document;
            var filter = new RoofSelectionFilter();

            Reference refA;
            try
            {
                refA = uidoc.Selection.PickObject(ObjectType.Element, filter, "Select Roof A (first roof to compare)");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            RoofBase roofA = doc.GetElement(refA) as RoofBase;
            if (roofA == null)
            {
                TaskDialog.Show("Compare Roofs", "Selected element is not a valid roof.");
                return Result.Cancelled;
            }

            Reference refB;
            try
            {
                refB = uidoc.Selection.PickObject(ObjectType.Element, filter, "Select Roof B (second roof to compare)");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            RoofBase roofB = doc.GetElement(refB) as RoofBase;
            if (roofB == null)
            {
                TaskDialog.Show("Compare Roofs", "Selected element is not a valid roof.");
                return Result.Cancelled;
            }

            if (roofA.Id == roofB.Id)
            {
                TaskDialog.Show("Compare Roofs", "Please select two different roofs.");
                return Result.Cancelled;
            }

            var win = new RoofComparisonWindow(uidoc, data.Application, roofA.Id, roofB.Id);
            win.Show();

            return Result.Succeeded;
        }
    }
}
