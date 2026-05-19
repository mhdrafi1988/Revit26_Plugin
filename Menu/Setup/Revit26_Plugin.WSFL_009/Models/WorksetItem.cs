using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.WSFL_009.Models
{
    public partial class WorksetItem : ObservableObject
    {
        [ObservableProperty]
        private int serialNumber;

        [ObservableProperty]
        private string linkName;

        [ObservableProperty]
        private string currentWorksetName = "None";

        [ObservableProperty]
        private bool isMixedWorkset;

        [ObservableProperty]
        private string proposedWorksetName;

        [ObservableProperty]
        private string proposedWorksetTooltip;

        [ObservableProperty]
        private bool isSelected;

        [ObservableProperty]
        private bool hasInstances = true;

        [ObservableProperty]
        private bool isExistingWorkset;

        [ObservableProperty]
        private string existingWorksetName;

        // Add this event - will be set by the ViewModel
        public event System.Action SelectionChanged;

        partial void OnIsSelectedChanged(bool value)
        {
            System.Diagnostics.Debug.WriteLine($"WorksetItem '{LinkName}' IsSelected changed to: {value}");
            SelectionChanged?.Invoke();
        }
    }
}