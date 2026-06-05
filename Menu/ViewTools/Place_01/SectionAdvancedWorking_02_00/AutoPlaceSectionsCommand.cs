using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.SectionPlacer.MVVM;
using System;

namespace Revit22_Plugin.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AutoPlaceSectionsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // ✅ Initialize the ExternalEvent + Handler
                SectionPlacerEventManager.Initialize();

                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                if (uidoc == null)
                {
                    TaskDialog.Show("Error", "No active Revit document found.");
                    return Result.Failed;
                }

                // ✅ Ensure only one window instance at a time
                var window = new AutoPlaceSectionsWindow(uidoc);
                window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", "Failed to open Auto Section Placer:\n" + ex.Message);
                return Result.Failed;
            }
        }
    }
}
