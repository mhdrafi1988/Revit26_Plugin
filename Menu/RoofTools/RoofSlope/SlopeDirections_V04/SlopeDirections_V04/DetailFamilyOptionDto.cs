using Autodesk.Revit.DB;

namespace Revit_26.CornertoDrainArrow_V05
{
    public sealed class DetailFamilyOptionDto
    {
        public ElementId SymbolId { get; init; }
        public string DisplayName { get; init; }
        public bool IsLineBased { get; init; }

        public override string ToString() => DisplayName;
    }
}
