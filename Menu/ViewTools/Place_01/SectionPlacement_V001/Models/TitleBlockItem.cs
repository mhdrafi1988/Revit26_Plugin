using Autodesk.Revit.DB;

namespace Revit26_Plugin.SectionPlacement_V07.Models
{
    public class TitleBlockItem
    {
        public ElementId SymbolId { get; }
        public string Name { get; }

        public TitleBlockItem(FamilySymbol symbol)
        {
            SymbolId = symbol.Id;
            Name = symbol.Name;
        }
    }
}
