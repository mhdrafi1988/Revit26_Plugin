using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.V5_00.UI.ViewModels;
using Revit26_Plugin.V5_00.UI.Views;
using System;

namespace Revit26_Plugin.V5_00.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AutoSlopeCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uidoc = uiApp.ActiveUIDocument;
                Document doc = uidoc.Document;

                Reference roofRef;
                try
                {
                    roofRef = uidoc.Selection.PickObject(
                        ObjectType.Element,
                        new RoofSelectionFilter(),
                        "Select a roof");
                }
                catch
                {
                    return Result.Cancelled;
                }

                var roof = doc.GetElement(roofRef) as RoofBase;
                if (roof == null)
                {
                    TaskDialog.Show("Error", "Selected element is not a roof.");
                    return Result.Failed;
                }

                // 🔴 CRITICAL – restore old working behavior
                PrepareRoofGeometry(roof);

                var vm = new RoofSlopeMainViewModel(uiApp, roof);
                var window = new MainWindow(vm)
                {
                    Topmost = true
                };

                window.ShowDialog();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }

        // ================= OLD ASD LOGIC RESTORED =================
        private static void PrepareRoofGeometry(RoofBase roof)
        {
            var doc = roof.Document;

            using (Transaction tx = new Transaction(doc, "Prepare Roof Geometry"))
            {
                tx.Start();

                var editor = roof.GetSlabShapeEditor();
                if (!editor.IsEnabled)
                    editor.Enable();

                foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                {
                    editor.ModifySubElement(v, 0.0);
                }

                tx.Commit();
            }
        }
    }

    internal class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is RoofBase;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
