using Autodesk.Revit.DB;

namespace Revit26_Plugin.Shared.Models
{
    /// <summary>
    /// Lightweight display wrapper for ComboBox binding (Name + backing ElementId).
    /// Shared across all tools — moved from DtlLineDim V006 (Core/Models/ComboItem.cs)
    /// during the V007 shared-infra refactor. Tool-local copies should be removed
    /// in favor of this one when touched.
    /// </summary>
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
