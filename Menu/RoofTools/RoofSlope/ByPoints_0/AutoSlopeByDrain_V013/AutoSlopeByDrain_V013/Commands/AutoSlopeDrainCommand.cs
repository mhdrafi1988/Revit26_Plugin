// =======================================================
// File: AutoSlopeDrainCommand.cs
// Namespace: Revit26_Plugin.MultiRoofSlopeByDrain.V013.Commands
// Ribbon string: "Revit26_Plugin.MultiRoofSlopeByDrain.V013.Commands.AutoSlopeByDrain"
// Changes vs V012:
//   - Picking roofs no longer changes the model. V012 enabled shape editing
//     and reset every vertex to 0 here, one committed transaction per roof,
//     outside any group: closing the window without running left the roofs
//     flattened, and a re-run after a cancel did not reset again. The reset
//     now happens at the start of each roof's run, inside that roof's group
//     (see AutoSlopeDrainEngine), so it rolls back with a failure or cancel.
//   - Body runs through ToolGuard.RunCommand (CLAUDE.md: whole body guarded).
//   - One window at a time: an open window is brought to the front.
//   - Roofs already selected (all FootPrintRoofs) are used without asking.
//   - The owner is set in the window's constructor.
// =======================================================

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.MultiRoofSlopeByDrain.V013.Core.Models;
using Revit26_Plugin.MultiRoofSlopeByDrain.V013.Core.Services;
using Revit26_Plugin.MultiRoofSlopeByDrain.V013.UI.ViewModels;
using Revit26_Plugin.MultiRoofSlopeByDrain.V013.UI.Views;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.Shared.Services;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.MultiRoofSlopeByDrain.V013.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AutoSlopeByDrain : IExternalCommand
    {
        private const string Title = "AutoSlope By Drain";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements) =>
            ToolGuard.RunCommand(GetType(), commandData, ref message, elements, ExecuteCore);

        private static Result ExecuteCore(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            if (AutoSlopeByDrainWindow.TryActivateOpenWindow())
                return Result.Succeeded;

            UIApplication uiApp = commandData.Application;
            UIDocument uidoc = uiApp.ActiveUIDocument;
            if (uidoc == null)
            {
                TaskDialog.Show(Title, "Open a project first.");
                return Result.Cancelled;
            }
            Document doc = uidoc.Document;

            // ── 1. Roofs: use the selection when it is only FootPrintRoofs ──
            List<FootPrintRoof> roofs = uidoc.Selection.GetElementIds()
                .Select(id => doc.GetElement(id))
                .OfType<FootPrintRoof>()
                .ToList();
            bool selectionIsRoofsOnly = roofs.Count > 0 && roofs.Count == uidoc.Selection.GetElementIds().Count;

            if (!selectionIsRoofsOnly)
            {
                // Esc throws OperationCanceledException; ToolGuard returns Cancelled.
                IList<Reference> roofRefs = uidoc.Selection.PickObjects(
                    ObjectType.Element,
                    new RoofFilter(),
                    "Select one or more footprint roofs (sketch-based, so openings can be read), then click Finish");

                roofs = roofRefs
                    .Select(r => r.ElementId)
                    .Distinct()
                    .Select(id => doc.GetElement(id))
                    .OfType<FootPrintRoof>()
                    .ToList();
            }

            if (roofs.Count == 0)
                return Result.Cancelled;

            // ── 2. Read each roof (read-only: nothing is changed here) ─────
            var diagnostics = new List<LogEntry>();
            var detectionService = new DrainDetectionService();
            var roofDataList = new List<RoofData>();

            foreach (FootPrintRoof roof in roofs)
            {
                var roofData = new RoofData { Roof = roof };
                roofData.TopFace = RoofGeometryService.GetTopFace(roof);
                if (roofData.TopFace == null)
                {
                    diagnostics.Add(new LogEntry(LogLevel.Warning,
                        $"Skipped roof {roof.Name} (Id {roof.Id.Value}): its top face could not be found."));
                    continue;
                }

                // Shape-edit points, when shape editing is already on. Used only
                // to weight the progress bar; the run reads them again fresh.
                SlabShapeEditor editor = roof.GetSlabShapeEditor();
                if (editor != null && editor.IsEnabled)
                    foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                        roofData.Vertices.Add(v);

                roofData.DetectedDrains = detectionService.DetectDrainsFromRoof(
                    roof, roofData.TopFace, null, entry => diagnostics.Add(entry));

                roofDataList.Add(roofData);
            }

            if (roofDataList.Count == 0)
            {
                TaskDialog.Show(Title, "None of the selected roofs could be read (no top face found). Nothing to open.");
                return Result.Cancelled;
            }

            // ── 3. Window (modeless, owned by Revit) ───────────────────────
            var viewModel = new AutoSlopeDrainViewModel(uidoc, uiApp, roofDataList, diagnostics);
            var window = new AutoSlopeByDrainWindow(viewModel, uiApp);
            window.Show();
            return Result.Succeeded;
        }
    }

    /// <summary>
    /// Only FootPrintRoof can be picked: opening detection reads the roof's
    /// sketch, and only footprint roofs have one.
    /// </summary>
    public class RoofFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is FootPrintRoof;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
