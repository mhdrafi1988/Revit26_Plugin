using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.DeleteLineStyles.V001.Core.Models
{
    /// <summary>
    /// One custom line style (a user subcategory of Lines) in the Delete Line Styles grid.
    /// </summary>
    public partial class LineStyleRow : ObservableObject
    {
        /// <summary>Id of the line style's subcategory; deleting it deletes the line style.</summary>
        public ElementId CategoryId { get; }

        /// <summary>Line style name as shown in Revit's Line Styles dialog.</summary>
        public string Name { get; }

        /// <summary>Number of lines (model, detail, symbolic and sketch lines) using this style.</summary>
        public int LineCount { get; }

        /// <summary>True when at least one line uses this style.</summary>
        public bool IsUsed => LineCount > 0;

        /// <summary>Why the style can never be deleted (owned by another user, …); empty when it can.</summary>
        public string BlockReason { get; }

        /// <summary>True when <see cref="BlockReason"/> is set.</summary>
        public bool IsBlocked => !string.IsNullOrEmpty(BlockReason);

        /// <summary>Badge text: Unused, Used or Blocked.</summary>
        public string Status => IsBlocked ? "Blocked" : IsUsed ? "Used" : "Unused";

        /// <summary>Whether the row can be ticked in the current mode.</summary>
        [ObservableProperty] private bool isDeletable;

        /// <summary>Ticked for deletion.</summary>
        [ObservableProperty] private bool isSelected;

        /// <summary>Creates a row.</summary>
        public LineStyleRow(ElementId categoryId, string name, int lineCount, string blockReason)
        {
            CategoryId = categoryId;
            Name = name;
            LineCount = lineCount;
            BlockReason = blockReason ?? string.Empty;
        }

        /// <summary>
        /// Updates <see cref="IsDeletable"/> for the mode: unused-only allows unused styles,
        /// all-custom allows every style that is not blocked. Unticks the row if it is no longer allowed.
        /// </summary>
        public void ApplyMode(bool includeUsed)
        {
            IsDeletable = !IsBlocked && (includeUsed || !IsUsed);
            if (!IsDeletable) IsSelected = false;
        }

        /// <summary>Tooltip explaining the status.</summary>
        public string StatusTip => IsBlocked
            ? BlockReason
            : IsUsed
                ? $"{LineCount} line(s) use this style. Deleting it moves them to the replacement style."
                : "No line uses this style.";
    }
}
