using Autodesk.Revit.DB;

namespace Revit26_Plugin.DtlLineDim_V03.Models
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
