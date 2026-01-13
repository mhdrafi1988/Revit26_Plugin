using Autodesk.Revit.DB;

namespace Revit26_Plugin.SectionPlacement_V07.Models
{
    public class SectionItem
    {
        public ElementId ViewId { get; }
        public string Name { get; }

        public SectionItem(ViewSection view)
        {
            ViewId = view.Id;
            Name = view.Name;
        }
    }
}
