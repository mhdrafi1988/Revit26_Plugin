using Autodesk.Revit.Attributes;
using Revit26_Plugin.RoofLoopAnalyzerPDC.V005.Infrastructure.ExternalEvents;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.RoofLoopAnalyzerPDC.V005.UI.ViewModels;
using Revit26_Plugin.RoofLoopAnalyzerPDC.V005.UI.Views;
using System;

namespace Revit26_Plugin.RoofLoopAnalyzerPDC.V005.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RoofLoopAnalyzerPDCCommand : IExternalCommand
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
                var vm = new RoofLoopAnalyzerPDCViewModel(doc, roof);

                // ── IExternalEventHandler + ExternalEvent.Create() must happen inside
                //    Execute(), i.e. while we are still in a valid Revit API context.
                var handler = new RoofLoopAnalyzerPDCEventHandler(vm);
                var externalEvent = ExternalEvent.Create(handler);
                vm.SetExternalEvent(externalEvent, handler);
                // Launch WPF window
                var window = new RoofLoopAnalyzerPDCWindow
                {
                    DataContext = vm,
                    Topmost     = true
                };

                // Modeless: the window stays open and never blocks Revit.
                window.Show();

                // Queue the first analysis through the external event now that the
                // window is up — Revit runs it on its own thread when idle.
                vm.AnalyzeCommand.Execute(null);

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
