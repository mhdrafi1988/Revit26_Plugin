using System;
using System.Windows.Interop;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Revit26_Plugin.RoofDrainCalloutPlacing.VByDrain.V004.Helpers;
using Revit26_Plugin.RoofDrainCalloutPlacing.VByDrain.V004.Models;
using Revit26_Plugin.RoofDrainCalloutPlacing.VByDrain.V004.Services;
using Revit26_Plugin.RoofDrainCalloutPlacing.VByDrain.V004.ViewModels;
using Revit26_Plugin.RoofDrainCalloutPlacing.VByDrain.V004.Views;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.VByDrain.V004.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RoofDrainCalloutPlacingCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData.Application;
                var uiDoc = uiApp.ActiveUIDocument;
                var doc = uiDoc.Document;

                if (uiDoc == null || doc == null)
                {
                    TaskDialog.Show("Error", "No active document.");
                    return Result.Failed;
                }

                // Step 1: Prompt user to select a roof
                var selection = uiDoc.Selection;
                Reference roofRef;
                
                try
                {
                    roofRef = selection.PickObject(
                        ObjectType.Element,
                        new RoofSelectionFilter(),
                        "Select a roof to place callouts on");
                }
                catch (OperationCanceledException)
                {
                    return Result.Cancelled;
                }

                if (roofRef == null)
                {
                    return Result.Cancelled;
                }

                var roof = doc.GetElement(roofRef) as RoofBase;
                if (roof == null)
                {
                    TaskDialog.Show("Error", "Selected element is not a roof.");
                    return Result.Failed;
                }

                // Step 2: Auto-detect all inner loop openings on the roof
                var detectedOpenings = RoofOpeningDetectionService.DetectOpeningsOnRoof(roof, doc, null);

                if (detectedOpenings.Count == 0)
                {
                    TaskDialog.Show("No Openings", "No inner loop openings detected on this roof.");
                    return Result.Cancelled;
                }

                // Step 3: Load settings
                var settings = SettingsService.LoadSettings();
                var settingsService = new SettingsService();

                // Step 4: Create ViewModel and Window
                var viewModel = new RoofDrainCalloutPlacingViewModel(
                    uiApp,
                    roof,
                    detectedOpenings,
                    settings,
                    settingsService);

                var window = new RoofDrainCalloutPlacingWindow
                {
                    DataContext = viewModel
                };

                // Step 5: Set window owner to Revit main window
                var windowHelper = new WindowInteropHelper(window);
                windowHelper.Owner = uiApp.MainWindowHandle;

                // Step 6: Show window (modeless)
                window.Show();

                return Result.Succeeded;
            }
            catch (OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Command failed: {ex.Message}");
                return Result.Failed;
            }
        }
    }
}
