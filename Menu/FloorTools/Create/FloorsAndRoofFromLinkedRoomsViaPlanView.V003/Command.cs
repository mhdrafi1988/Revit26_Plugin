using System;
using System.Linq;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRoomsViaPlanView.V003
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Command : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uidoc = commandData.Application.ActiveUIDocument;
            var doc = uidoc.Document;

            // Step 1 — must be a plan view.
            if (doc.ActiveView is not ViewPlan planView)
            {
                TaskDialog.Show(
                    "Floors and Roofs From Linked Rooms",
                    "This tool only runs from a plan view. Open a floor plan and try again.");
                return Result.Cancelled;
            }

            Level targetLevel = planView.GenLevel;
            string levelFallbackNote = null;

            if (targetLevel == null)
            {
                var levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .ToList();

                targetLevel = levels.FirstOrDefault(l =>
                    l.Name.Replace(" ", "").Equals("Level1", StringComparison.OrdinalIgnoreCase));

                if (targetLevel != null)
                {
                    levelFallbackNote = "Active view has no associated level — falling back to 'Level 1'.";
                }
                else if (levels.Count > 0)
                {
                    targetLevel = levels[0];
                    levelFallbackNote = $"Active view has no associated level and no 'Level 1' exists — falling back to the lowest level in the model ('{targetLevel.Name}').";
                }
                else
                {
                    TaskDialog.Show(
                        "Floors and Roofs From Linked Rooms",
                        "This plan view has no associated level, and the model has no levels at all to fall back to. Open a standard floor plan and try again.");
                    return Result.Cancelled;
                }
            }

            var handler = new RunCreateElementsExternalEventHandler();
            var externalEvent = ExternalEvent.Create(handler);

            var viewModel = new MainViewModel(doc, planView, targetLevel, levelFallbackNote, handler, externalEvent);
            handler.ViewModel = viewModel;

            var window = new FloorsFromLinkedRoomsWindow { DataContext = viewModel };
            new WindowInteropHelper(window).Owner = commandData.Application.MainWindowHandle;
            window.Show();

            return Result.Succeeded;
        }
    }
}
