using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.RoofCreateTest.V001.UI;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;

namespace Revit26_Plugin.RoofCreateTest.V001
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RoofCreateTestCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Document doc = uiApp.ActiveUIDocument?.Document;

            if (doc == null)
            {
                TaskDialog.Show("RoofCreateTest", "No active document.");
                return Result.Cancelled;
            }

            List<Level> levels = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .ToList();

            List<RoofType> roofTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .Cast<RoofType>()
                .Where(rt => rt.GetCompoundStructure() != null)
                .OrderBy(rt => rt.Name)
                .ToList();

            if (levels.Count == 0 || roofTypes.Count == 0)
            {
                TaskDialog.Show("RoofCreateTest",
                    $"Cannot start: {levels.Count} level(s), {roofTypes.Count} valid roof type(s) found.");
                return Result.Cancelled;
            }

            var vm = new RoofCreateTestViewModel(uiApp, levels, roofTypes);
            var window = new RoofCreateTestWindow { DataContext = vm };

            new WindowInteropHelper(window)
            {
                Owner = commandData.Application.MainWindowHandle
            };

            window.Show();
            return Result.Succeeded;
        }
    }
}