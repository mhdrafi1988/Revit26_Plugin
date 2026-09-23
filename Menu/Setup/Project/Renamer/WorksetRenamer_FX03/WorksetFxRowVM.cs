using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.WorksetRenamer.FX03.ViewModels
{
    /// <summary>What this row represents and what Update will do with it.</summary>
    public enum RowKind
    {
        /// <summary>Old Name matched an existing workset — will be renamed.</summary>
        Rename,
        /// <summary>Old Name had no match in the model — will create a new workset (opt-in via checkbox).</summary>
        CreateNew,
        /// <summary>An existing model workset with no row in the Excel file — informational only.</summary>
        Unmatched,
        /// <summary>Row cannot be actioned (e.g. blank New Name).</summary>
        Invalid
    }

    public enum RowRenameStatus { Pending, Renamed, Created, Unchanged, Error }

    public partial class WorksetFxRowVM : ObservableObject
    {
        public RowKind Kind { get; }

        /// <summary>Existing workset id. Null for CreateNew rows — the workset doesn't exist yet.</summary>
        public WorksetId WorksetId { get; }

        /// <summary>
        /// Current model workset name (Rename/Unmatched rows), or the unmatched
        /// Excel "Old Name" text as read from the file (CreateNew/Invalid rows).
        /// </summary>
        public string OldName { get; }

        [ObservableProperty]
        private string newName;

        [ObservableProperty]
        private bool isSelected;

        /// <summary>Set by duplicate re-validation; true once NewName was auto-suffixed to stay unique.</summary>
        [ObservableProperty]
        private bool isDuplicateWarning;

        [ObservableProperty]
        private RowRenameStatus status = RowRenameStatus.Pending;

        [ObservableProperty]
        private string errorMessage;

        /// <summary>Whether this row can be checked/edited/acted on by Update.</summary>
        public bool IsActionable => Kind == RowKind.Rename || Kind == RowKind.CreateNew;

        public WorksetFxRowVM(RowKind kind, WorksetId worksetId, string oldName, string newName)
        {
            Kind = kind;
            WorksetId = worksetId;
            OldName = oldName;
            this.newName = newName ?? string.Empty;
            isSelected = kind == RowKind.Rename || kind == RowKind.CreateNew;
        }
    }
}
