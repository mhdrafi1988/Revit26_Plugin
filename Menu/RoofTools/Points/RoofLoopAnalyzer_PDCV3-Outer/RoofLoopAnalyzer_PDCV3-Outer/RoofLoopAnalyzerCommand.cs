using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.PDCV3.ViewModels;
using Revit26_Plugin.PDCV3.Views;
using System;

namespace Revit26_Plugin.PDCV3.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RoofLoopAnalyzerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument    uidoc = uiapp.ActiveUIDocument;
            Document      doc   = uidoc.Document;

            try
            {
                // Select roof
                Reference pickedRef = uidoc.Selection.PickObject(ObjectType.Element, "Select a RoofBase element");
                if (pickedRef == null) return Result.Cancelled;

                RoofBase roof = doc.GetElement(pickedRef) as RoofBase;
                if (roof == null)
                {
                    TaskDialog.Show("Error", "Selected element is not a RoofBase.");
                    return Result.Failed;
                }

                // Enable shape editing & flatten all vertices to Z = 0
                using (Transaction tx = new Transaction(doc, "Enable Shape Editing"))
                {
                    tx.Start();

                    SlabShapeEditor editor = roof.GetSlabShapeEditor();
                    if (!editor.IsEnabled)
                        editor.Enable();

                    foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                        editor.ModifySubElement(v, 0.0);

                    tx.Commit();
                }

                // Build ViewModel and auto-analyze
                var vm = new RoofLoopAnalyzerViewModel(doc, roof);
                vm.AnalyzeCommand.Execute(null);

                // Launch WPF window
                var window = new RoofLoopAnalyzerWindow
                {
                    DataContext = vm,
                    Topmost     = true
                };

                window.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Exception", ex.Message);
                return Result.Failed;
            }
        }
    }
}
