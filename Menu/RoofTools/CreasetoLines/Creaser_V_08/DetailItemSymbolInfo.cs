using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_V08.Commands.Models
{
    public class DetailItemSymbolInfo
    {
        public FamilySymbol Symbol { get; }
        public string DisplayName { get; }

        public DetailItemSymbolInfo(FamilySymbol symbol)
        {
            Symbol = symbol;
            DisplayName = $"{symbol.Family.Name} : {symbol.Name}";
        }

        public override string ToString() => DisplayName;
    }
}
