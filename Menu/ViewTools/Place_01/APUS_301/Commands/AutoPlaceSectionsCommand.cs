using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
//using Revit22_Plugin.SectionPlacer.MVVM;
using Revit26_Plugin.APUS_301.MVVM;
using System;

namespace Revit26_Plugin.APUS_301.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class AutoPlaceSectionsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                SectionPlacerEventManager.Initialize();

                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                if (uidoc == null)
                {
                    TaskDialog.Show("Error", "No active document.");
                    return Result.Failed;
                }

                var window = new AutoPlaceSectionsWindow(uidoc);
                window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
                return Result.Failed;
            }
        }
    }
}
