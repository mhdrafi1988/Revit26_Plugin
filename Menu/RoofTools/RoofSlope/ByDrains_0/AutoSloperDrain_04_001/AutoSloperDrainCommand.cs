using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit22_Plugin.Asd_V4_01.ViewModels;
using Revit22_Plugin.Asd_V4_01.Views;
using System;
using System.Windows;

namespace Revit22_Plugin.Asd_V4_01.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AutoSloperDrain_04_01 : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uidoc = uiApp.ActiveUIDocument;
                Document doc = uidoc.Document;

                TaskDialog.Show("Roof Selection", "Please select a roof...");

                Reference roofRef;
                try
                {
                    roofRef = uidoc.Selection.PickObject(
                        ObjectType.Element,
                        new RoofSelectionFilter(),
                        "Select a roof"
                    );
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

                InitializeRoofGeometry(roof, doc);

                //var viewModel = new MainViewModel(uiApp, roof);
                var viewModel = new RoofSlopeMainViewModel(uiApp, roof);
                var mainWindow = new MainWindow(viewModel)
                {
                    Topmost = true
                };

                mainWindow.ShowDialog();

                return Result.Succeeded;
            }
            catch (Exception ex)
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

                var slabShapeEditor = roof.GetSlabShapeEditor();
                if (!slabShapeEditor.IsEnabled)
                {
                    slabShapeEditor.Enable();
                }

                foreach (SlabShapeVertex vertex in slabShapeEditor.SlabShapeVertices)
                {
                    slabShapeEditor.ModifySubElement(vertex, 0.0);
                }

                tx.Commit();
            }
        }
    }

    public class RoofSelectionFilter : ISelectionFilter
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
