using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V60.ViewModels;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V60.Views;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V60.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RoofRidgeCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiDoc = commandData.Application.ActiveUIDocument;
                if (uiDoc == null)
                {
                    message = "No active document found.";
                    return Result.Failed;
                }

                // 1. Select Roof ONLY – no drain picker
                RoofBase selectedRoof = null;
                try
                {
                    var roofRef = uiDoc.Selection.PickObject(
                        ObjectType.Element,
                        new RoofSelectionFilter(),
                        "Select a roof element");
                    selectedRoof = uiDoc.Document.GetElement(roofRef) as RoofBase;
                    if (selectedRoof == null)
                    {
                        message = "Selected element is not a valid roof.";
                        return Result.Failed;
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }

                // 2. Launch UI – pass the selected roof.
                // The ViewModel will auto‑load openings upon construction.
                var viewModel = new RoofRidgeViewModel(uiDoc, selectedRoof);
                var view = new RoofRidgeView(viewModel);
                viewModel.SetOwnerWindow(view);
                view.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Roof Ridge Command failed: {ex.Message}";
                return Result.Failed;
            }
        }

        private class RoofSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is RoofBase;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}