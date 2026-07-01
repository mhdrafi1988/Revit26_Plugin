using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.DivideInnerLoops.V006.ViewModels;
using Revit26_Plugin.DivideInnerLoops.V006.Views;
using System;

namespace Revit26_Plugin.DivideInnerLoops.V006
{
    /// <summary>
    /// External command that launches the Inner Loop Divider V005 tool.
    /// Prompts the user to pick a roof, enables shape editing, then opens the window.
    /// No TaskDialogs are raised; all feedback is surfaced in the activity log.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RoofLoopAnalyzerCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document   doc   = uidoc.Document;

            try
            {
                // ── Pick roof ────────────────────────────────────────────────
                Reference pickedRef = uidoc.Selection.PickObject(
                    ObjectType.Element, "Select a RoofBase element");

                if (pickedRef == null) return Result.Cancelled;

                RoofBase roof = doc.GetElement(pickedRef) as RoofBase;
                if (roof == null)
                {
                    message = "Selected element is not a RoofBase.";
                    return Result.Failed;
                }

                // ── Enable shape editing & flatten ───────────────────────────
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

                // ── Init ViewModel & auto-analyze ────────────────────────────
                var vm = new RoofLoopAnalyzerViewModel(doc, roof);
                vm.AnalyzeCommand.Execute(null);

                // ── Launch UI ────────────────────────────────────────────────
                var window = new RoofLoopAnalyzerWindow
                {
                    DataContext = vm,
                    Topmost     = true
                };

                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
