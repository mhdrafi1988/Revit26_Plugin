using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.LinesFromMechanical.V010.Services;
using Revit26_Plugin.LinesFromMechanical.V010.ViewModels;
using Revit26_Plugin.LinesFromMechanical.V010.Views;
using System;
using System.Linq;
using System.Windows;

namespace Revit26_Plugin.LinesFromMechanical.V010.Commands;

[Transaction(TransactionMode.Manual)]
[Regeneration(RegenerationOption.Manual)]
public class CreateLinkedMechanicalCirclesCommand : IExternalCommand
{
    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    {
        try
        {
            if (commandData == null)
            { message = "Command data is null"; return Result.Failed; }

            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            if (uiDoc == null)
            { message = "No active document. Please open a project."; return Result.Failed; }

            Document doc = uiDoc.Document;
            if (doc == null)
            { message = "Document is null"; return Result.Failed; }

            if (doc.ActiveView is not ViewPlan viewPlan)
            { message = "Active view must be a plan view."; return Result.Failed; }

            if (doc.ActiveView.IsTemplate)
            { message = "Active view cannot be a view template."; return Result.Failed; }

            try { CircleIdentityStorage.Initialize(); }
            catch (Exception initEx)
            { message = $"Failed to initialize storage schema: {initEx.Message}"; return Result.Failed; }

            var viewModel = new MainWindowViewModel(uiDoc, doc, viewPlan);
            var window    = new MainWindow
            {
                DataContext           = viewModel,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                Owner                 = GetRevitWindow()
            };

            window.ShowDialog();
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

    private static Window? GetRevitWindow()
    {
        try
        {
            return Application.Current.Windows
                .OfType<Window>()
                .FirstOrDefault(w => w.IsActive && w.GetType().Name.Contains("Revit"));
        }
        catch { return null; }
    }
}
