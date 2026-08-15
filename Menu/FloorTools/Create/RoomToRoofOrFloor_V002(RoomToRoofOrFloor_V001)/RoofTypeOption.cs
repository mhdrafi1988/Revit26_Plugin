using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoomToRoofOrFloor.V002.Core.Models
{
    /// <summary>
    /// Display wrapper for a RoofType, used to populate the single
    /// "Roof Type" dropdown that applies to every selected room.
    /// </summary>
    public class RoofTypeOption
    {
        public ElementId TypeId { get; }
        public string Name { get; }

        public RoofTypeOption(ElementId typeId, string name)
        {
            TypeId = typeId;
            Name = name;
        }

        public override string ToString() => Name;
    }
}
