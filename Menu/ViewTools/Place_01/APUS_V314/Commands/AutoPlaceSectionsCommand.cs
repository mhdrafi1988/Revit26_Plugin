// File: AutoPlaceSectionsCommand.cs
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_V314.ExternalEvents;
using Revit26_Plugin.APUS_V314.Views;
using System;

namespace Revit26_Plugin.APUS_V314.Commands
{
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
                UIApplication uiApp = commandData.Application;
                UIDocument uidoc = uiApp.ActiveUIDocument;

                if (uidoc == null || uidoc.Document == null)
                {
                    TaskDialog.Show("APUS V314 – Error", "No active Revit document found.");
                    return Result.Failed;
                }

                // Check if document is a project (not family)
                if (uidoc.Document.IsFamilyDocument)
                {
                    TaskDialog.Show("APUS V314 – Error",
                        "This command only works in project documents, not family documents.");
                    return Result.Failed;
                }

                // Initialize event manager
                AutoPlaceSectionsEventManager.Initialize();

                // Show window
                var window = new AutoPlaceSectionsWindow(uidoc);
                window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("APUS V314 – Error",
                    $"Failed to start Auto Place Sections:\n{ex.Message}\n\n{ex.StackTrace}");
                return Result.Failed;
            }
        }
    }
}