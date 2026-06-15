using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V315.DependencyInjection;
using Revit26_Plugin.APUS_V315.ViewModels.Main;
using Revit26_Plugin.APUS_V315.Views.Windows;
using System;

namespace Revit26_Plugin.APUS_V315.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class AutoPlaceSectionsCommand : IExternalCommand
{
    public Result Execute(
        ExternalCommandData commandData,
        ref string message,
        ElementSet elements)
    {
        try
        {
            var uiApp = commandData.Application;
            var uidoc = uiApp.ActiveUIDocument;

            if (uidoc?.Document == null)
            {
                TaskDialog.Show("APUS V315", "No active Revit document found.");
                return Result.Failed;
            }

            if (uidoc.Document.IsFamilyDocument)
            {
                TaskDialog.Show("APUS V315",
                    "This command only works in project documents, not family documents.");
                return Result.Failed;
            }

            // Initialize service locator with the current UIDocument
            ServiceLocator.Initialize(uidoc);

            // Get ViewModel from DI container
            var viewModel = ServiceLocator.Get<AutoPlaceSectionsViewModel>();

            // Create and show window
            var window = new AutoPlaceSectionsWindow(viewModel);
            window.Show();

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            TaskDialog.Show("APUS V315 – Error",
                $"Failed to start Auto Place Sections:\n{ex.Message}");
            return Result.Failed;
        }
    }
}