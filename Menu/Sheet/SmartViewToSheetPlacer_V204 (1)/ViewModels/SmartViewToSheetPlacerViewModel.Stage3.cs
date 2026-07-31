using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V204.ViewModels
{
    /// <summary>Stage 3: Suggested Placement — badges + editable grid (the packed
    /// SheetGroups/ViewPlacements are already populated by Stage 2's RunPacking;
    /// this stage is mostly display + navigation, editing happens via bindings
    /// directly on AllPlacements/SuggestedSheets).</summary>
    public partial class SmartViewToSheetPlacerViewModel
    {
        // ---- Stage 3 state ----
        [ObservableProperty] private bool _stage3Complete;
        public string Stage3StatusLabel => Stage3Complete ? "Complete" : (Stage2Complete ? "In Progress" : "Not Started");

        [RelayCommand]
        private void BackToStage2()
        {
            Stage3Expanded = false;
            Stage2Expanded = true;
        }

        [RelayCommand]
        private void NextToStage4()
        {
            Stage3Complete = true;
            Stage3Expanded = false;
            Stage4Expanded = true;
        }

        partial void OnStage3CompleteChanged(bool value)
        {
            OnPropertyChanged(nameof(Stage3StatusLabel));
            OnPropertyChanged(nameof(Stage4StatusLabel));
        }
    }
}
