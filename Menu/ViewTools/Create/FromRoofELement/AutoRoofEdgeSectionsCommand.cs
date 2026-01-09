using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.AutoRoofSections.MVVM;
using System;

namespace Revit22_Plugin.AutoRoofSections.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AutoRoofEdgeSectionsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiapp = commandData.Application;
                UIDocument uidoc = uiapp.ActiveUIDocument;

                if (uidoc == null)
                {
                    TaskDialog.Show("Error", "No active Revit document found.");
                    return Result.Failed;
                }

                // Init ExternalEvent
                RoofSectionsEventManager.Initialize();

                // Open UI
                var win = new RoofSectionWindow(uidoc, uiapp);
                win.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", "Failed to open Auto Roof Edge Sections:\n" + ex.Message);
                return Result.Failed;
            }
        }
    }
}
