using CommunityToolkit.Mvvm.ComponentModel;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.DeleteWorkset.V001.Core.Models
{
    /// <summary>
    /// Single row in the Delete Workset data grid.
    /// </summary>
    public partial class WorksetRow : ObservableObject
    {
        public WorksetId WorksetId { get; }

        public string Name { get; }

        /// <summary>Number of elements on this workset.</summary>
        public int ElementCount { get; }

        /// <summary>Revit owner name, or empty string when unowned.</summary>
        public string Owner { get; }

        /// <summary>True when the workset is open in the current session.</summary>
        public bool IsOpen { get; }

        /// <summary>
        /// False when another user owns the workset, some of its elements are owned by other
        /// users, or it is the only user workset — the checkbox is disabled for these.
        /// </summary>
        public bool IsDeletable { get; }

        /// <summary>Human-readable reason the workset cannot be deleted, shown as a tooltip.</summary>
        public string NonDeletableReason { get; }

        /// <summary>Short badge text shown next to the name of a non-deletable workset.</summary>
        public string StatusBadge { get; }

        [ObservableProperty] private bool isSelected;

        public WorksetRow(
            WorksetId id,
            string name,
            int elementCount,
            string owner,
            bool isOpen,
            bool isDeletable,
            string nonDeletableReason = null,
            string statusBadge = null)
        {
            WorksetId = id;
            Name = name;
            ElementCount = elementCount;
            Owner = owner;
            IsOpen = isOpen;
            IsDeletable = isDeletable;
            NonDeletableReason = nonDeletableReason ?? string.Empty;
            StatusBadge = statusBadge ?? string.Empty;
        }
    }
}
