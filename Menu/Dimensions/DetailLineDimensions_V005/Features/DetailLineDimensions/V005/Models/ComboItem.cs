using Autodesk.Revit.DB;

namespace Revit26_Plugin.DetailLIneDimensions.V005.Models
{
    public class ComboItem
    {
        public string Name { get; }
        public ElementId ElementId { get; }

        public ComboItem(string name, ElementId id)
        {
            Name = name;
            ElementId = id;
        }

        public override string ToString() => Name;
    }
}
