using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit22_Plugin.Asd.ViewModels;
using Revit22_Plugin.Asd.Views;
using System.Windows;

namespace Revit22_Plugin.Asd.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AutoSloperDrain_04 : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uidoc = uiApp.ActiveUIDocument;
                Document doc = uidoc.Document;

                // Select roof first before showing UI
                TaskDialog.Show("Roof Selection", "Please select a roof...");

                Reference roofRef;
                try
                {
                    roofRef = uidoc.Selection.PickObject(
                        ObjectType.Element,
                        new RoofFilter(),
                        "Select a roof");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    TaskDialog.Show("Cancelled", "Roof selection was cancelled.");
                    return Result.Cancelled;
                }

                var roof = doc.GetElement(roofRef) as RoofBase;
                if (roof == null)
                {
                    TaskDialog.Show("Error", "Invalid roof selection.");
                    return Result.Failed;
                }

                // Initialize roof: enable shape editing and reset vertices to ZERO
                InitializeRoofGeometry(roof, doc);

                // Create and show main window with the selected roof
                var viewModel = new MainViewModel(uiApp, roof);
                var mainWindow = new MainWindow(viewModel)
                {
                    Topmost = true // Keep window always on top
                };

                mainWindow.ShowDialog();

                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                message = $"Failed to initialize plugin: {ex.Message}";
                return Result.Failed;
            }
        }

        private void InitializeRoofGeometry(RoofBase roof, Document doc)
        {
            using (Transaction tx = new Transaction(doc, "Initialize Roof Geometry"))
            {
                tx.Start();

                // Enable slab shape editing if needed
                var slabShapeEditor = roof.GetSlabShapeEditor();
                if (!slabShapeEditor.IsEnabled)
                {
                    slabShapeEditor.Enable();
                }

                // Reset ALL vertices to ZERO elevation (local coordinates)
                foreach (SlabShapeVertex vertex in slabShapeEditor.SlabShapeVertices)
                {
                    slabShapeEditor.ModifySubElement(vertex, 0.0);
                }

                tx.Commit();
            }
        }
    }

    // Roof Filter class
    public class RoofFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is RoofBase;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}