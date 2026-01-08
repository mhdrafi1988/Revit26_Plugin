// File: RoofRidgeLinesCommand.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.Commands
//
// Responsibility:
// - Revit ExternalCommand entry point
// - Validate Revit context
// - Launch WPF wizard window
// - NO geometry, NO transactions, NO business logic

using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

using Revit26_Plugin.RoofRidgeLines_V06.Services.Context;
using Revit26_Plugin.RoofRidgeLines_V06.Views;

namespace Revit26_Plugin.RoofRidgeLines_V06.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class RoofRidgeLinesCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            // 1. Validate UIApplication
            UIApplication uiApp = commandData?.Application;
            if (uiApp == null)
            {
                message = "UIApplication is null.";
                return Result.Failed;
            }

            // 2. Validate UIDocument
            UIDocument uiDoc = uiApp.ActiveUIDocument;
            if (uiDoc == null)
            {
                message = "No active document found.";
                return Result.Failed;
            }

            // 3. Validate Document
            Document doc = uiDoc.Document;
            if (doc == null)
            {
                message = "Document is null.";
                return Result.Failed;
            }

            try
            {
                // 4. Create Revit context service (no validation yet)
                RevitContextService contextService =
                    new RevitContextService(uiApp, uiDoc, doc);

                // 5. Create and show wizard window (WPF)
                MainWizardWindow window = new MainWizardWindow(contextService);

                // Revit-safe modal dialog
                window.ShowDialog();

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // User cancelled safely
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                // Any unexpected failure
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
