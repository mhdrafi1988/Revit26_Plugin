// File: Commands/AutoPlaceSectionsCommand.cs
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V330.Services;
using Revit26_Plugin.APUS_V330.ViewModels;
using Revit26_Plugin.APUS_V330.Views;
using System;
using System.Linq;

namespace Revit26_Plugin.APUS_V330.Commands
{
    /// <summary>
    /// Revit command entry point. ALL Revit API calls happen here, on the Revit thread.
    /// The UI is created only after data is fully loaded.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class AutoPlaceSectionsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
            => Execute(commandData.Application, ref message, elements, out _);

        public Result Execute(
            UIApplication uiApp,
            ref string    message,
            ElementSet    elements,
            out AutoPlaceSectionsWindow createdWindow)
        {
            createdWindow = null;
            try
            {
                var uidoc = uiApp.ActiveUIDocument;
                if (uidoc?.Document == null)
                {
                    TaskDialog.Show("APUS V330", "No active Revit document found.");
                    return Result.Failed;
                }

                if (uidoc.Document.IsFamilyDocument)
                {
                    TaskDialog.Show("APUS V330",
                        "This command only works in project documents, not family documents.");
                    return Result.Failed;
                }

                // ---- ALL REVIT API CALLS HERE (Revit thread) ----
                var sections     = new SectionCollectionService(uidoc.Document).Collect();
                var titleBlocks  = new TitleBlockCollectionService(uidoc.Document).Collect();

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

                // ---- UI CREATION (no Revit API calls beyond this point) ----
                var viewModel = new AutoPlaceSectionsViewModel(
                    uidoc, sections, titleBlocks, sheetNumbers, placementScopes);

                createdWindow = new AutoPlaceSectionsWindow(viewModel);
                createdWindow.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("APUS V330 – Error",
                    $"Failed to start Auto Place Sections:\n{ex.Message}");
                return Result.Failed;
            }
        }

        public static Result Invoke(UIApplication uiApp)
        {
            var command  = new AutoPlaceSectionsCommand();
            string msg   = null;
            var elements = new ElementSet();
            return command.Execute(uiApp, ref msg, elements, out _);
        }
    }
}
