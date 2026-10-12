using Autodesk.Revit.DB;

namespace Revit26_Plugin.Shared.Models
{
    /// <summary>Name/ElementId pair for ComboBox items bound via DisplayMemberPath="Name".</summary>
    public class ComboItem
    {
        public string Name { get; }
        public ElementId ElementId { get; }

        public ComboItem(string name, ElementId elementId)
        {
            Name = name;
            ElementId = elementId;
        }
    }
}
