using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.WSFL_010.Models
{
    /// <summary>
    /// Which of the three display grids this item belongs to.
    /// Computed once during LoadItems and used by the view's CollectionViewSource filters.
    /// </summary>
    public enum WorksetGridCategory
    {
        /// <summary>
        /// Grid 1 — proposed workset already exists AND all instances are assigned to it (exact match).
        /// </summary>
        AlreadyAssigned,

        /// <summary>
        /// Grid 2 — link has instances but the proposed workset does not exist yet (actionable).
        /// </summary>
        NeedsWorkset,

        /// <summary>
        /// Grid 3 — no instances found in the model (unloaded / not placed).
        /// </summary>
        NoInstances
    }

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

        /// <summary>
        /// True when the proposed workset name already exists in the document
        /// AND every instance of this link is already assigned to that workset.
        /// This is the Grid 1 condition (exact match).
        /// </summary>
        [ObservableProperty]
        private bool isExactMatchAssigned;

        /// <summary>
        /// True when the proposed workset name already exists in the document
        /// but the assignment is wrong / partial. Kept for tooltip / info use.
        /// </summary>
        [ObservableProperty]
        private bool isExistingWorkset;

        [ObservableProperty]
        private string existingWorksetName;

        /// <summary>Number of RevitLinkInstance elements found in the model.</summary>
        [ObservableProperty]
        private int instanceCount;

        /// <summary>Grid bucket — set by the ViewModel during LoadItems.</summary>
        [ObservableProperty]
        private WorksetGridCategory gridCategory;

        public event System.Action SelectionChanged;

        partial void OnIsSelectedChanged(bool value)
        {
            System.Diagnostics.Debug.WriteLine($"WorksetItem '{LinkName}' IsSelected changed to: {value}");
            SelectionChanged?.Invoke();
        }
    }
}
