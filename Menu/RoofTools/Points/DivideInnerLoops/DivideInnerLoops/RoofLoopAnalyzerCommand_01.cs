using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using Revit22_Plugin.PDCV1.Views;
using Revit22_Plugin.PDCV1.ViewModels;

namespace Revit22_Plugin.PDCV1.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RoofLoopAnalyzerCommand_01 : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

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

                // Enable shape editing & flatten
                using (Transaction tx = new Transaction(doc, "Enable Shape Editing"))
                {
                    tx.Start();

                    var editor = roof.GetSlabShapeEditor();
                    if (!editor.IsEnabled)
                        editor.Enable();

                    foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                        editor.ModifySubElement(v, 0.0);

                    tx.Commit();
                }

                // Initialize ViewModel & auto-analyze
                var vm = new RoofLoopAnalyzerViewModel(doc, roof);
                vm.AnalyzeCommand.Execute(null);

                // Launch UI
                var window = new RoofLoopAnalyzerWindow();
                window.DataContext = vm;

                window.Topmost = true;
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
