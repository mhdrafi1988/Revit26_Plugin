using Autodesk.Revit.DB;

namespace Revit26_Plugin.SlopeDirections.V005
{
    public sealed class DetailFamilyOptionDto
    {
        public ElementId SymbolId { get; init; }
        public string DisplayName { get; init; }
        public bool IsLineBased { get; init; }

        public override string ToString() => DisplayName;
    }
}
