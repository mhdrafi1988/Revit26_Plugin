using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit22_Plugin.SectionManagerMVVM_Refactored
{
    [Transaction(TransactionMode.Manual)]
    public class SectionManagerCommandRefactored : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                SectionManagerEventManagerRefactored.Initialize();

                UIDocument uidoc = commandData.Application.ActiveUIDocument;
                var window = new SectionsListWindowRefactored(uidoc);
                window.Show();

                return Result.Succeeded;
            }
            catch (System.Exception ex)
            {
                TaskDialog.Show("Error", "Failed to open Refactored Section Manager:\n" + ex.Message);
                return Result.Failed;
            }
        }
    }
}
