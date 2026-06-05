// File: AutoPlaceSectionsCommand.cs
// V320 — All namespaces corrected. Fully qualified types eliminate ambiguity.

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V320.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V320.Commands
{
    /// <summary>
    /// Revit command entry point for Auto Place Sections.
    /// ALL Revit API calls happen HERE, on the Revit thread.
    /// UI is created AFTER data is loaded — no Revit API calls after Show().
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AutoPlaceSectionsCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            return Execute(commandData.Application, ref message, elements, out _);
        }

        /// <summary>
        /// Main execution overload. Also used by the refresh operation.
        /// Uses fully qualified type names throughout to prevent any
        /// ambiguity between APUS_V318 and APUS_V320 types.
        /// </summary>
        public Result Execute(
            UIApplication uiApp,
            ref string message,
            ElementSet elements,
            out Revit26_Plugin.APUS_V320.Views.AutoPlaceSectionsWindow createdWindow)
        {
            createdWindow = null;

            try
            {
                UIDocument uidoc = uiApp.ActiveUIDocument;

                if (uidoc == null || uidoc.Document == null)
                {
                    TaskDialog.Show("APUS V320", "No active Revit document found.");
                    return Result.Failed;
                }

                if (uidoc.Document.IsFamilyDocument)
                {
                    TaskDialog.Show("APUS V320",
                        "This command only works in project documents, not family documents.");
                    return Result.Failed;
                }

                // ── ALL REVIT API CALLS HERE (Revit thread — safe) ──────────

                // 1. Load sections with verified placement status
                var sectionService = new SectionCollectionService(uidoc.Document);
                List<Revit26_Plugin.APUS_V320.ViewModels.SectionItemViewModel> sections
                    = sectionService.Collect();

                // 2. Load available title blocks
                var titleBlockService = new TitleBlockCollectionService(uidoc.Document);
                var titleBlocks = titleBlockService.Collect();

                // 3. Derive filter lists from collected data (no extra API calls)
                List<string> sheetNumbers = sections
                    .Where(s => !string.IsNullOrEmpty(s.SheetNumber))
                    .Select(s => s.SheetNumber)
                    .Distinct()
                    .OrderBy(s => s, StringComparer.Ordinal)
                    .ToList();

                List<string> placementScopes = sections
                    .Where(s => !string.IsNullOrEmpty(s.PlacementScope))
                    .Select(s => s.PlacementScope)
                    .Distinct()
                    .OrderBy(s => s, StringComparer.Ordinal)
                    .ToList();

                // ── UI CREATION — no Revit API calls beyond this point ───────

                var viewModel =
                    new Revit26_Plugin.APUS_V320.ViewModels.AutoPlaceSectionsViewModel(
                        uidoc,
                        sections,
                        titleBlocks,
                        sheetNumbers,
                        placementScopes);

                createdWindow =
                    new Revit26_Plugin.APUS_V320.Views.AutoPlaceSectionsWindow(viewModel);

                createdWindow.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("APUS V320 — Error",
                    $"Failed to start Auto Place Sections:\n{ex.Message}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Invokes the command programmatically (e.g. from a ribbon refresh button).
        /// </summary>
        public static Result Invoke(UIApplication uiApp)
        {
            var command = new AutoPlaceSectionsCommand();
            string message = null;
            var elements = new ElementSet();
            return command.Execute(uiApp, ref message, elements, out _);
        }

        public string GetName() => "APUS V320 — Auto Place Sections";
    }
}