using Autodesk.Revit.DB;

namespace Revit26_Plugin.AutomatedSectionPlacer.V001.Models
{
    /// <summary>
    /// One existing ViewSheet in the project, offered in a suggested
    /// sheet-group's "Target Sheet" dropdown as an alternative to "Create New
    /// Sheet". Loaded alongside titleblocks in the handler's LoadViews pass.
    /// </summary>
    public class ExistingSheetOption
    {
        public ElementId SheetId { get; }
        public string SheetNumber { get; }
        public string SheetName { get; }
        public string Label => $"{SheetNumber} - {SheetName}";

        public ExistingSheetOption(ElementId sheetId, string sheetNumber, string sheetName)
        {
            SheetId = sheetId;
            SheetNumber = sheetNumber;
            SheetName = sheetName;
        }
    }
}
