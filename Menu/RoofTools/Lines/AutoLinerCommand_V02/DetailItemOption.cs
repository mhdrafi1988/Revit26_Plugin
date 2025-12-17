using Autodesk.Revit.DB;

namespace Revit26_Plugin.AutoLiner_V02.Models
{
    public class DetailItemOption
    {
        public FamilySymbol Symbol { get; }
        public string Name => $"{Symbol.Family.Name} : {Symbol.Name}";

        public DetailItemOption(FamilySymbol symbol)
        {
            Symbol = symbol;
        }
    }
}
