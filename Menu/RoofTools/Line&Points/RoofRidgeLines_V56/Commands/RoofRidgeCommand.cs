using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V56.Services;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V56.ViewModels;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V56.Views;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V56.Commands
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

                // 1. Select Roof
                RoofBase selectedRoof = null;
                try
                {
                    var roofRef = uiDoc.Selection.PickObject(
                        Autodesk.Revit.UI.Selection.ObjectType.Element,
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

                // 2. Select Drain Points
                List<XYZ> drainPoints;
                try
                {
                    drainPoints = DrainPointPicker.PickDrainPoints(uiDoc, selectedRoof);
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }
                catch (Exception ex)
                {
                    message = $"Drain selection failed: {ex.Message}";
                    return Result.Failed;
                }

                if (drainPoints.Count < 2)
                {
                    message = "At least two drain points are required.";
                    return Result.Failed;
                }

                // 3. Launch UI
                var viewModel = new RoofRidgeViewModel(uiDoc, selectedRoof, drainPoints);
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

        private class RoofSelectionFilter : Autodesk.Revit.UI.Selection.ISelectionFilter
        {
            public bool AllowElement(Element elem) => elem is RoofBase;
            public bool AllowReference(Reference reference, XYZ position) => false;
        }
    }
}
