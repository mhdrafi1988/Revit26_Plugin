using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.SectionAutoRenamer.09.ViewModels;

public partial class SectionItemViewModel : ObservableObject
{
    // ── Immutable Revit data ────────────────────────────────────────────────
    public ElementId ElementId   { get; }
    public string    OriginalName { get; }
    public string    SheetNumber  { get; }
    public string    DetailNumber { get; }
    public bool      IsPinned     { get; }

    public bool IsPlaced => !string.IsNullOrWhiteSpace(SheetNumber);

    // ── Observable mutable state ────────────────────────────────────────────
    [ObservableProperty] private bool   isSelected;
    [ObservableProperty] private string editableName;
    [ObservableProperty] private string previewName;
    [ObservableProperty] private bool   isDuplicate;

    public SectionItemViewModel(ViewSection section)
    {
        ElementId    = section.Id;
        OriginalName = section.Name;
        SheetNumber  = section.get_Parameter(BuiltInParameter.VIEWER_SHEET_NUMBER)?.AsString();
        DetailNumber = section.get_Parameter(BuiltInParameter.VIEWER_DETAIL_NUMBER)?.AsString();
        IsPinned     = section.Pinned;

        editableName = OriginalName;
        previewName  = OriginalName;
    }
}
