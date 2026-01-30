using Autodesk.Revit.DB;

namespace Revit26_Plugin.DwgSymbolicConverter_V02.Models
{
    public class DwgItemModel
    {
        public ElementId ElementId { get; }
        public string TypeName { get; }

        public DwgItemModel(ImportInstance importInstance, string sourceLabel)
        {
            ElementId = importInstance.Id;
            TypeName = sourceLabel;
        }

        public override string ToString()
        {
            return $"{TypeName} ({ElementId})";
        }
    }
}
