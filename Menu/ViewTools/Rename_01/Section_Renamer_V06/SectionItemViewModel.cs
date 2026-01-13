using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.SARV6.ViewModels;

public partial class SectionItemViewModel : ObservableObject
{
    public ElementId ElementId { get; }
    public string OriginalName { get; }
    public string SheetNumber { get; }
    public string DetailNumber { get; }
    public int Serial { get; }

    public bool IsPlaced => !string.IsNullOrWhiteSpace(SheetNumber);

    [ObservableProperty] private string editableName;
    [ObservableProperty] private string previewName;
    [ObservableProperty] private bool isDuplicate;

    public SectionItemViewModel(ViewSection section, int serial)
    {
        ElementId = section.Id;
        OriginalName = section.Name;
        SheetNumber = section.get_Parameter(BuiltInParameter.VIEWER_SHEET_NUMBER)?.AsString();
        DetailNumber = section.get_Parameter(BuiltInParameter.VIEWER_DETAIL_NUMBER)?.AsString();
        Serial = serial;

        editableName = OriginalName;
        previewName = OriginalName;
    }
}
