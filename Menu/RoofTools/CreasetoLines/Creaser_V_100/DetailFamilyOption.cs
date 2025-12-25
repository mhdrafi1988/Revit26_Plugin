using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_V100.Models
{
    /// <summary>
    /// Represents a selectable line-based detail family symbol.
    /// </summary>
    public class DetailFamilyOption
    {
        public string Name { get; }
        public ElementId SymbolId { get; }

        public DetailFamilyOption(string name, ElementId symbolId)
        {
            Name = name;
            SymbolId = symbolId;
        }

        public override string ToString() => Name;
    }
}
