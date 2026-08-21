using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Models
{
    /// <summary>
    /// UI-bindable wrapper around a single RevitLinkInstance found in the host document.
    /// Populated by LinkService (Phase 2) — Phase 1 uses this purely as a display/selection
    /// model with no live Revit API calls behind it.
    /// </summary>
    public partial class LinkedModelItem : ObservableObject
    {
        /// <summary>ElementId.Value of the RevitLinkInstance in the host document.</summary>
        public long LinkInstanceId { get; set; }

        /// <summary>Display name of the link instance (e.g. "Architecture").</summary>
        public string InstanceName { get; set; } = string.Empty;

        /// <summary>File name of the linked document (e.g. "Architecture.rvt").</summary>
        public string DocumentTitle { get; set; } = string.Empty;

        /// <summary>True if the link is currently loaded and its document is readable.</summary>
        public bool IsLoaded { get; set; }

        /// <summary>
        /// Checkbox-bound selection state. Multiple links may be checked simultaneously —
        /// checked links are merged into a single combined category tree in Section 2
        /// (see MainViewModel remarks on merged-tree design).
        /// </summary>
        [ObservableProperty]
        private bool _isSelected;
    }
}
