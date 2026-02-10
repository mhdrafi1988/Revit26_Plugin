// File: TitleBlockItemViewModel.cs
using Autodesk.Revit.DB;
using Revit26_Plugin.SectionManager_V07.ViewModels;

namespace Revit26_Plugin.APUS_V314.Models
{
    public class TitleBlockItemViewModel : BaseViewModel
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

        public override string ToString()
        {
            return DisplayName;
        }
    }
}