using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.WorksetManager_02.Models
{
    /// <summary>
    /// Represents a linked file for which NO matching workset was found.
    /// Grid 3 — actionable: user can check to create workset + assign instances.
    /// </summary>
    public partial class UnmatchedLinkItem : ObservableObject
    {
        public string LinkedFileName    { get; set; } = string.Empty;

        /// <summary>The workset name that will be created: "+Link {BaseName}".</summary>
        public string ProposedWorkset   { get; set; } = string.Empty;

        public int    InstanceCount     { get; set; }

        [ObservableProperty]
        private bool _isChecked;
    }
}
