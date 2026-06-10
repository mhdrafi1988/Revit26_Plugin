using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.PonitOnCurvesInnerandOuter.V01.ViewModels;
using Revit26_Plugin.PonitOnCurvesInnerandOuter.V01.Views;
using System;

namespace Revit26_Plugin.PonitOnCurvesInnerandOuter.V01.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RoofLoopAnalyzerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            // 1. Pick a roof — cancellation throws, never returns null
            Reference pickedRef;
            try
            {
                pickedRef = uidoc.Selection.PickObject(ObjectType.Element, "Select a RoofBase element");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }

            RoofBase roof = doc.GetElement(pickedRef) as RoofBase;
            if (roof == null)
            {
                message = "Selected element is not a RoofBase.";
                return Result.Failed;
            }

            // 2. Enable shape editing and flatten all vertices to Z = 0
            try
            {
                using (Transaction tx = new Transaction(doc, "Enable Shape Editing"))
                {
                    tx.Start();

                    SlabShapeEditor editor = roof.GetSlabShapeEditor();
                    if (!editor.IsEnabled) editor.Enable();

                    foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                        editor.ModifySubElement(v, 0.0);

                    tx.Commit();
                }
            }
            catch (Exception ex)
            {
                message = $"Failed to enable shape editing: {ex.Message}";
                return Result.Failed;
            }

            // 3. Build ViewModel, auto-analyze, open window
            try
            {
                var vm = new RoofLoopAnalyzerViewModel(doc, roof);
                vm.AnalyzeCommand.Execute(null);

                var window = new RoofLoopAnalyzerWindow
                {
                    DataContext = vm,
                    Topmost = true
                };

                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = $"Unexpected error: {ex.Message}";
                return Result.Failed;
            }
        }
    }
}