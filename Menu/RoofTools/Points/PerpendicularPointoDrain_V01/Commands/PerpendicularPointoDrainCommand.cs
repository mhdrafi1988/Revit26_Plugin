using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.PerpendicularPointoDrain.V01.ViewModels;
using Revit26_Plugin.PerpendicularPointoDrain.V01.Views;
using System;

namespace Revit26_Plugin.PerpendicularPointoDrain.V01.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class PerpendicularPointoDrainCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument    uidoc = uiapp.ActiveUIDocument;
            Document      doc   = uidoc.Document;

            try
            {
                Reference pickedRef = uidoc.Selection.PickObject(ObjectType.Element, "Select a RoofBase element");
                if (pickedRef == null) return Result.Cancelled;

                RoofBase roof = doc.GetElement(pickedRef) as RoofBase;
                if (roof == null)
                {
                    TaskDialog.Show("Error", "Selected element is not a RoofBase.");
                    return Result.Failed;
                }

                // Enable shape editing WITHOUT resetting existing vertices to Z = 0.
                // Unlike RoofLoopAnalyzerCommand, this tool adds points on top of whatever
                // shape already exists — it must never flatten the roof first.
                using (Transaction tx = new Transaction(doc, "Enable Shape Editing"))
                {
                    tx.Start();

                    SlabShapeEditor editor = roof.GetSlabShapeEditor();
                    if (!editor.IsEnabled)
                        editor.Enable();

                    tx.Commit();
                }

                var vm = new PerpendicularPointoDrainViewModel(doc, uidoc, roof);

                var window = new PerpendicularPointoDrainWindow
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
