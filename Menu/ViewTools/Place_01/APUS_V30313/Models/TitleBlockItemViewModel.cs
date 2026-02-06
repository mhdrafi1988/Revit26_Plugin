using Autodesk.Revit.DB;

namespace Revit26_Plugin.APUS_V313.Models
{
    public class TitleBlockItemViewModel
    {
        public FamilySymbol FamilySymbol { get; }
        public string DisplayName => $"{FamilySymbol.FamilyName} - {FamilySymbol.Name}";

        public TitleBlockItemViewModel(FamilySymbol familySymbol)
        {
            FamilySymbol = familySymbol;
        }
    }
}