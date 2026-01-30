using Autodesk.Revit.DB;

namespace Revit26_Plugin.DwgSymbolicConverter_V02.Models
{
    /// <summary>
    /// Represents a selectable CAD Import or Link in the family.
    /// </summary>
    public class CadImportItem
    {
        public ElementId ElementId { get; }
        public string Name { get; }
        public string ImportType { get; }
        public ImportInstance ImportInstance { get; }

        public CadImportItem(
            ImportInstance importInstance,
            string importType)
        {
            ImportInstance = importInstance;
            ElementId = importInstance.Id;
            Name = importInstance.Name;
            ImportType = importType;
        }

        public override string ToString()
        {
            return $"{Name} ({ImportType})";
        }
    }
}
