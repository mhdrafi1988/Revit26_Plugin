using Autodesk.Revit.DB;

namespace Revit26_Plugin.APUS_V311.Models
{
    /// <summary>
    /// UI-safe wrapper for a Title Block FamilySymbol.
    /// </summary>
    public class TitleBlockItemViewModel
    {
        public ElementId SymbolId { get; }
        public string FamilyName { get; }
        public string TypeName { get; }

        public string DisplayName => $"{FamilyName} : {TypeName}";

        public TitleBlockItemViewModel(FamilySymbol symbol)
        {
            SymbolId = symbol.Id;
            FamilyName = symbol.FamilyName;
            TypeName = symbol.Name;
        }
    }
}
