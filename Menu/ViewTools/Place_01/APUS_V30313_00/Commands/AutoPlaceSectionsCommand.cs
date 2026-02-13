using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V313.ExternalEvents;
using Revit26_Plugin.APUS_V313.Views;
using System;

namespace Revit26_Plugin.APUS_V313.Commands
{
    /// <summary>
    /// Revit entry point for Auto Place Sections (APUS).
    /// Responsible ONLY for:
    /// - Validating Revit context
    /// - Initializing ExternalEvent infrastructure
    /// - Opening the WPF UI
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class AutoPlaceSectionsCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            try
            {
                // ===================== VALIDATE CONTEXT =====================

                UIApplication uiApp = commandData.Application;
                UIDocument uidoc = uiApp.ActiveUIDocument;

                if (uidoc == null || uidoc.Document == null)
                {
                    TaskDialog.Show(
                        "APUS – Error",
                        "No active Revit document found.");
                    return Result.Failed;
                }

                // ===================== INIT EXTERNAL EVENT =====================

                var _ = AutoPlaceSectionsEventManager.ExternalEvent;

                // ===================== SHOW UI =====================

                var window = new AutoPlaceSectionsWindow(uidoc);
                window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show(
                    "APUS – Error",
                    $"Failed to start Auto Place Sections:\n{ex.Message}");

                return Result.Failed;
            }
        }
    }
}