using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.ImportCadLines.Services;
using Revit22_Plugin.ImportCadLines.ViewModels;
using Revit22_Plugin.ImportCadLines.Views;

namespace Revit22_Plugin.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CadImportCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var doc = commandData.Application.ActiveUIDocument.Document;
            var view = doc.ActiveView;

            var vm = new CadLayerSelectionViewModel();
            var service = new CadImportService(doc, view);

            if (!service.TryGetCadLayers(out var layers))
            {
                TaskDialog.Show("CAD Import", "Failed to retrieve CAD layers.");
                return Result.Failed;
            }

            foreach (var layer in layers)
                vm.Layers.Add(layer);

            var window = new CadLayerSelectionWindow(vm);
            if (window.ShowDialog() == true)
            {
                service.ImportLinesFromSelectedLayer(vm.SelectedLayer);
                return Result.Succeeded;
            }

            return Result.Cancelled;
        }
    }
}
