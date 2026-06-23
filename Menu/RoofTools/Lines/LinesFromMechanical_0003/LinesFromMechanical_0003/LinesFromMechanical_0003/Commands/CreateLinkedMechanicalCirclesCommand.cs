using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.LinesFromMechanical.V003.Services;
using Revit26_Plugin.LinesFromMechanical.V003.ViewModels;
using Revit26_Plugin.LinesFromMechanical.V003.Views;
using System;
using System.Linq;
using System.Windows;

namespace Revit26_Plugin.LinesFromMechanical.V003.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CreateLinkedMechanicalCirclesCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            // Validate command data
            if (commandData == null)
            {
                message = "Command data is null";
                return Result.Failed;
            }

            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null)
            {
                message = "No active document. Please open a project.";
                return Result.Failed;
            }

            Document doc = uiDoc.Document;
            if (doc == null)
            {
                message = "Document is null";
                return Result.Failed;
            }

            View activeView = doc.ActiveView;
            if (activeView == null)
            {
                message = "No active view found.";
                return Result.Failed;
            }

            // Validate view type
            if (activeView is not ViewPlan viewPlan)
            {
                message = "Active view must be a plan view.";
                return Result.Failed;
            }

            if (activeView.IsTemplate)
            {
                message = "Active view cannot be a view template.";
                return Result.Failed;
            }

            // Initialize schema BEFORE any transaction is opened
            try
            {
                CircleIdentityStorage.Initialize();
            }
            catch (Exception initEx)
            {
                message = $"Failed to initialize storage schema: {initEx.Message}";
                return Result.Failed;
            }

            // Create and show the window
            var viewModel = new MainWindowViewModel(uiDoc, doc, viewPlan);
            var window = new MainWindow
            {
                DataContext = viewModel,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Owner = GetRevitWindow()
            };

            window.ShowDialog();

            // Clean up
            viewModel.Dispose();

            return Result.Succeeded;
        }
        catch (Exception ex)
        {
            message = $"Command failed: {ex.Message}\n\nStack Trace: {ex.StackTrace}";
            System.Diagnostics.Debug.WriteLine($"ERROR: {ex}");
            return Result.Failed;
        }
    }

    private Window GetRevitWindow()
    {
        try
        {
            return Application.Current.Windows
                .OfType<Window>()
                .FirstOrDefault(w => w.IsActive && w.GetType().Name.Contains("Revit"));
        }
        catch
        {
            return null;
        }
    }
}