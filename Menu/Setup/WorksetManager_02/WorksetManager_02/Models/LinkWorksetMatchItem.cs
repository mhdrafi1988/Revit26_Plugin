using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.WorksetManager_02.Models
{
    /// <summary>
    /// Represents a linked file where a matching workset was found
    /// but one or more instances are NOT yet assigned to it.
    /// Grid 2 — actionable: user can check and reassign.
    /// </summary>
    public partial class LinkWorksetMatchItem : ObservableObject
    {
        public string LinkedFileName           { get; set; } = string.Empty;
        public string MatchedWorkset           { get; set; } = string.Empty;
        public int    TotalInstances           { get; set; }
        public int    InstancesOnCorrectWorkset { get; set; }
        public int    InstancesOnWrongWorkset   { get; set; }

        [ObservableProperty]
        private bool _isChecked;

        /// <summary>Display label for assignment status column.</summary>
        public string AssignmentStatus =>
            InstancesOnWrongWorkset == 0
                ? "✔ All Correct"
                : $"{InstancesOnWrongWorkset} need reassignment";
    }
}
