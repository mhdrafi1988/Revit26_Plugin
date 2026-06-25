using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V64.ViewModels;
using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V64.Views;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V64.Commands
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
                // The ViewModel will auto-load openings upon construction.
                var viewModel = new RoofRidgeViewModel(uiDoc, selectedRoof);
                var view = new RoofRidgeView(viewModel);
                viewModel.SetOwnerWindow(view);
                view.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                // For XAML/BAML load failures, ex.Message itself is always the same
                // generic "Provide value on '...MarkupExtension' threw an exception"
                // wrapper — WPF's loader adds it automatically whenever a markup
                // extension's ProvideValue() throws, and it never names the actual
                // resource/property/value at fault. It carries no diagnostic value,
                // so it's dropped here in favor of the real InnerException chain.
                var sb = new System.Text.StringBuilder("Roof Ridge Command failed:");
                var inner = ex.InnerException ?? ex;
                int depth = 0;
                while (inner != null && depth < 6)
                {
                    sb.Append($"\n  [{inner.GetType().Name}] {inner.Message}");
                    inner = inner.InnerException;
                    depth++;
                }
                message = sb.ToString();
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
