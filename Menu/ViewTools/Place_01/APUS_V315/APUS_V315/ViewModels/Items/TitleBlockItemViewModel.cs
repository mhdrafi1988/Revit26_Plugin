using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows.Media;

namespace Revit26_Plugin.APUS_V315.ViewModels.Items;

public sealed partial class TitleBlockItemViewModel : ObservableObject
{
    [ObservableProperty]
    private ElementId _symbolId;

    [ObservableProperty]
    private string _familyName = string.Empty;

    [ObservableProperty]
    private string _typeName = string.Empty;

    public string DisplayName => $"{FamilyName} : {TypeName}";

    public TitleBlockItemViewModel(FamilySymbol symbol)
    {
        SymbolId = symbol.Id;
        FamilyName = symbol.FamilyName;
        TypeName = symbol.Name;
    }

    public override string ToString() => DisplayName;
}