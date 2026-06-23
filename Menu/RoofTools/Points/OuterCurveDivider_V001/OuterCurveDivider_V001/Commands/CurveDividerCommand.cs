using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.OuterCurveDivider.V001.ViewModels;
using Revit26_Plugin.OuterCurveDivider.V001.Views;
using System;

namespace Revit26_Plugin.OuterCurveDivider.V001.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CurveDividerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument    uidoc = uiapp.ActiveUIDocument;
            Document      doc   = uidoc.Document;

            try
            {
                Reference pickedRef = uidoc.Selection.PickObject(
                    ObjectType.Element, new RoofSelectionFilter(),
                    "Select a roof to divide its curved edges");
                if (pickedRef == null) return Result.Cancelled;

                RoofBase roof = doc.GetElement(pickedRef) as RoofBase;
                if (roof == null)
                {
                    TaskDialog.Show("Curve Point Divider", "Selected element is not a roof.");
                    return Result.Failed;
                }

                var vm = new CurveDividerViewModel(doc, roof);
                var window = new CurveDividerWindow { DataContext = vm, Topmost = true };
                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Curve Point Divider — Exception", ex.Message);
                return Result.Failed;
            }
        }
    }

    public class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is RoofBase;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
