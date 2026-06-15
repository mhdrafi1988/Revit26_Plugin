// File: AutoPlaceSectionsCommand.cs
// FIXED: Add proper command execution methods
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V314.Services;
using Revit26_Plugin.APUS_V314.ViewModels;
using Revit26_Plugin.APUS_V314.Views;
using System;
using System.Linq;

namespace Revit26_Plugin.APUS_V314.Commands
{
    /// <summary>
    /// Revit command entry point for Auto Place Sections.
    /// ALL Revit API calls happen HERE, on the Revit thread.
    /// UI is created AFTER data is loaded.
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

        // Additional overload for refresh operation
        public Result Execute(
            UIApplication uiApp,
            ref string message,
            ElementSet elements,
            out AutoPlaceSectionsWindow createdWindow)
        {
            createdWindow = null;

            try
            {
                UIDocument uidoc = uiApp.ActiveUIDocument;

                if (uidoc == null || uidoc.Document == null)
                {
                    TaskDialog.Show("APUS V314", "No active Revit document found.");
                    return Result.Failed;
                }

                if (uidoc.Document.IsFamilyDocument)
                {
                    TaskDialog.Show("APUS V314",
                        "This command only works in project documents, not family documents.");
                    return Result.Failed;
                }

                // --- ALL REVIT API CALLS HERE (Revit Thread, Safe) ---

                // 1. Load sections with VERIFIED placement status
                var sectionService = new SectionCollectionService(uidoc.Document);
                var sections = sectionService.Collect();

                // 2. Load title blocks
                var titleBlockService = new TitleBlockCollectionService(uidoc.Document);
                var titleBlocks = titleBlockService.Collect();

                // 3. Collect unique sheet numbers and placement scopes for filters
                var sheetNumbers = sections
                    .Where(s => !string.IsNullOrEmpty(s.SheetNumber))
                    .Select(s => s.SheetNumber)
                    .Distinct()
                    .OrderBy(s => s, StringComparer.Ordinal)
                    .ToList();

                var placementScopes = sections
                    .Where(s => !string.IsNullOrEmpty(s.PlacementScope))
                    .Select(s => s.PlacementScope)
                    .Distinct()
                    .OrderBy(s => s, StringComparer.Ordinal)
                    .ToList();

                // --- UI CREATION (No Revit API calls) ---

                // Create ViewModel with pre-loaded data
                var viewModel = new AutoPlaceSectionsViewModel(
                    uidoc,
                    sections,
                    titleBlocks,
                    sheetNumbers,
                    placementScopes);

                // Launch UI - NO Revit API calls beyond this point
                createdWindow = new AutoPlaceSectionsWindow(viewModel);
                createdWindow.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("APUS V314 – Error",
                    $"Failed to start Auto Place Sections:\n{ex.Message}");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Static method to invoke the command programmatically
        /// </summary>
        public static Result Invoke(UIApplication uiApp)
        {
            var command = new AutoPlaceSectionsCommand();
            string message = null;
            var elements = new ElementSet();
            return command.Execute(uiApp, ref message, elements, out _);
        }

        public string GetName() => "APUS V314 – Auto Place Sections";
    }
}