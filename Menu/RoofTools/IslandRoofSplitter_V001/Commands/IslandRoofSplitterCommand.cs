// IslandRoofSplitter V001 — entry point. Picks one sloped footprint roof, then the detail lines drawn over it in
// the active view, snapshots everything Revit-side into a SplitterContext, and opens the modeless window.
//
// Decisions (per Anas's confirmed decisions, 2026-10-14):
//  * New tool family (not a RoofFromDetailLines version): face projection, nesting and vertex-Z push are new.
//  * Nested regions: Ring / Full — user choice in the UI.     * Crossings: Split / Endpoint chains — user choice.
//  * Roof type / level / base offset: pickers, defaulting to the source roof.
//  * Chord tolerance: 5 mm default, 1–50 mm.                  * Sloped interior: boundary only / ridge & valley points.
//  * Mark: "<prefix>Island_NN", prefix defaults to "R<sourceRoofId>_" (clear it for plain Island_NN).
//  * Flat mode arcs: keep true arcs / tessellate — user choice.
//  * Preview (Run) is pure Core and auto-runs on open; nothing is created until Commit.
//
// Error handling goes through Shared ToolGuard (cancel → Cancelled, anything else → logged + dialog + Failed),
// so this file holds no try/catch of its own (refactor, 2026-10-14).
#nullable enable
using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.IslandRoofSplitter.V001.Infrastructure.Helpers;
using Revit26_Plugin.IslandRoofSplitter.V001.UI.ViewModels;
using Revit26_Plugin.IslandRoofSplitter.V001.UI.Views;
using Revit26_Plugin.Shared.Services;

namespace Revit26_Plugin.IslandRoofSplitter.V001.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class IslandRoofSplitterCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
            => ToolGuard.RunCommand(GetType(), commandData, ref message, elements, Run);

        private static Result Run(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument
                               ?? throw new InvalidOperationException("Open a project view first.");

            var roof = SelectionHelper.PickRoof(uidoc);
            var lines = SelectionHelper.PickDetailCurves(uidoc);
            var context = ContextFactory.Build(uidoc, roof, lines);

            var window = new IslandRoofSplitterWindow(new IslandRoofSplitterViewModel(context));
            new WindowInteropHelper(window) { Owner = uiapp.MainWindowHandle };
            window.Show();
            return Result.Succeeded;
        }
    }
}
