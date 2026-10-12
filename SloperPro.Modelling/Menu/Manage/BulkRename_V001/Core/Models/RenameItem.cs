using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.BulkRename.V001.Core.Models
{
    /// <summary>
    /// One renameable project item (a line style, line pattern, arrowhead or fill pattern) in the
    /// Bulk Rename grid.
    /// </summary>
    public sealed partial class RenameItem : ObservableObject
    {
        /// <summary>
        /// Id of the element whose <c>Name</c> is changed. For a line style this is its projection
        /// graphics style, because a line style's category has no settable name.
        /// </summary>
        public ElementId Id { get; }

        /// <summary>Name the item has in the model.</summary>
        public string CurrentName { get; }

        /// <summary>Extra info shown in the grid: "Drafting" / "Model" for fill patterns, the arrow style for arrowheads.</summary>
        public string Details { get; }

        /// <summary>How the item is used, for example "12 lines"; an em dash when not known.</summary>
        public string UsedBy { get; }

        /// <summary>Why the item can never be renamed (built-in, owned by another user); empty when it can.</summary>
        public string LockReason { get; }

        /// <summary>True when <see cref="LockReason"/> is set.</summary>
        public bool IsLocked => !string.IsNullOrEmpty(LockReason);

        /// <summary>Name typed or generated for the row; the item is renamed to it on Apply.</summary>
        [ObservableProperty] private string newName;

        /// <summary>Ticked: the rules act on this row and Apply renames it.</summary>
        [ObservableProperty] private bool isSelected;

        /// <summary>Row state shown in the Status column.</summary>
        [ObservableProperty] private RenameStatus status;

        /// <summary>Tooltip for the Status column: why the row is Invalid, Duplicate or Locked.</summary>
        [ObservableProperty] private string statusMessage = string.Empty;

        /// <summary>Creates a row. The new name starts equal to the current name.</summary>
        public RenameItem(ElementId id, string currentName, string details, string usedBy, string lockReason)
        {
            Id = id;
            CurrentName = currentName ?? string.Empty;
            Details = details ?? string.Empty;
            UsedBy = string.IsNullOrEmpty(usedBy) ? "—" : usedBy;
            LockReason = lockReason ?? string.Empty;

            // Assign the backing fields directly: no change notifications while constructing.
            newName = CurrentName;
            status = IsLocked ? RenameStatus.Locked : RenameStatus.Unchanged;
            statusMessage = LockReason;
        }
    }
}
