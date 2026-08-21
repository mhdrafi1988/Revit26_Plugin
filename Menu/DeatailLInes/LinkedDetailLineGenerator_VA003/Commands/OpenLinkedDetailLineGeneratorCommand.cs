using System;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.LinkedDetailLineGenerator.VA003.UI.Views;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Commands
{
    /// <summary>
    /// Ribbon PushButton entry point. Per spec Sections 4–5: validates the active view
    /// is a supported plan view AND at least one valid placed RevitLinkInstance exists
    /// BEFORE opening the WPF window. Both conditions must pass.
    ///
    /// PHASE 1 NOTE: link validation here only checks for RevitLinkInstance existence/
    /// placement/document accessibility at the Revit-API level — it does not yet feed
    /// live link data into the window (MainViewModel still uses sample data in Phase 1).
    /// This command wiring is included now so the validation gate pattern is correct
    /// and ready for Phase 2 to swap sample data for live LinkService queries.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class OpenLinkedDetailLineGeneratorCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            Document doc = uiDoc.Document;
            View activeView = uiDoc.ActiveView;

            // ── 4.1 Validate Active View ────────────────────────────────────
            if (!IsSupportedPlanView(activeView))
            {
                TaskDialog.Show(
                    "Linked Detail Line Generator",
                    "This command can only be run from a supported plan view.");
                return Result.Cancelled;
            }

            // ── 5. Validate Linked Revit Models ─────────────────────────────
            bool hasValidLink;
            try
            {
                hasValidLink = new FilteredElementCollector(doc)
                    .OfClass(typeof(RevitLinkInstance))
                    .Cast<RevitLinkInstance>()
                    .Any(li => IsValidPlacedLink(li));
            }
            catch (Exception ex)
            {
                message = $"Failed to validate linked models: {ex.Message}";
                return Result.Failed;
            }

            if (!hasValidLink)
            {
                TaskDialog.Show(
                    "Linked Detail Line Generator",
                    "No valid placed Revit link was found in the current project.");
                return Result.Cancelled;
            }

            // ── Open UI (modeless, matches suite convention) ───────────────
            try
            {
                var window = new MainWindow(uiApp);
                window.Show();
            }
            catch (Exception ex)
            {
                message = $"Failed to open Linked Detail Line Generator: {ex.Message}";
                return Result.Failed;
            }

            return Result.Succeeded;
        }

        /// <summary>Floor Plan and Structural Plan supported in V1 per spec Section 4.1.
        /// Ceiling Plans intentionally excluded pending view-range behavior validation.</summary>
        private static bool IsSupportedPlanView(View view)
        {
            if (view == null) return false;
            return view.ViewType == ViewType.FloorPlan
                || view.ViewType == ViewType.EngineeringPlan; // Structural Plan
        }

        /// <summary>A link is valid if it has a placed instance and a readable linked
        /// document. Unloaded links (GetLinkDocument() == null) are excluded — surfaced
        /// to the user as "Unloaded" status in the window's link list, not as candidates
        /// for processing.</summary>
        private static bool IsValidPlacedLink(RevitLinkInstance linkInstance)
        {
            try
            {
                Document linkedDoc = linkInstance.GetLinkDocument();
                return linkedDoc != null;
            }
            catch
            {
                return false;
            }
        }
    }
}
