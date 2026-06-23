using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using Revit26_Plugin.Tools.DivideInnerLoops.V002.Views;
using Revit26_Plugin.Tools.DivideInnerLoops.V002.ViewModels;

namespace Revit26_Plugin.Tools.DivideInnerLoops.V002
{
    /// <summary>
    /// External command that launches the Inner Loop Divider tool.
    /// Prompts the user to select a roof, enables shape editing, then displays
    /// the loop analysis and division UI.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RoofLoopAnalyzerCommand : IExternalCommand
    {
        /// <summary>
        /// Executes the roof loop analysis workflow: selection, shape editor setup, ViewModel init, UI launch.
        /// </summary>
        /// <param name="commandData">Revit external command data.</param>
        /// <param name="message">Output message if the command fails.</param>
        /// <param name="elements">Output element set if the command fails.</param>
        /// <returns>Result of the command execution.</returns>
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
