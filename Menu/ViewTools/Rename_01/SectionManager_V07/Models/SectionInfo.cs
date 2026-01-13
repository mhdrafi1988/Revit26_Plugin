using Autodesk.Revit.DB;

namespace Revit26_Plugin.SectionManager_V07.Models
{
    public class SectionInfo
    {
        public ElementId ElementId { get; }
        public string OriginalName { get; }
        public string NewName { get; set; }
        public string SheetNumber { get; set; }

        public SectionInfo(ElementId id, string name)
        {
            ElementId = id;
            OriginalName = name;
            NewName = name;
        }
    }
}
