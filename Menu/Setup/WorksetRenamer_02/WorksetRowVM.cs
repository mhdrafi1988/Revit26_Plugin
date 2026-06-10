using System.ComponentModel;
using System.Runtime.CompilerServices;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.WorksetRenamer_01.ViewModels
{
    public enum RenameStatus { Pending, Renamed, Unchanged, Error }

    public class WorksetRowVM : INotifyPropertyChanged
    {
        // ── Identity ──────────────────────────────────────────────────
        public WorksetId WorksetId { get; }

        /// <summary>Name captured when the window opens — used by Revert.</summary>
        public string OriginalName { get; }

        // ── Display-only current name ─────────────────────────────────
        private string _currentName;
        public string CurrentName
        {
            get => _currentName;
            set { _currentName = value; OnPropertyChanged(); }
        }

        // ── Editable proposed name ────────────────────────────────────
        private string _newName;
        public string NewName
        {
            get => _newName;
            set { _newName = value; OnPropertyChanged(); }
        }

        // ── DataGrid checkbox ─────────────────────────────────────────
        private bool _isSelected = true;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        // ── Read-only state indicators (Revit API cannot change these on a live doc) ──

        /// <summary>
        /// Whether the workset is currently open (geometry loaded).
        /// Read-only — open/close requires model reopen via WorksetConfiguration.
        /// Displayed in the UI as a status indicator only.
        /// </summary>
        public bool IsOpen { get; }

        /// <summary>
        /// Whether the current user owns (has checked out) this workset.
        /// Read-only — editability is managed by Revit's worksharing checkout,
        /// not programmable via WorksetTable on a live document.
        /// Displayed in the UI as a status indicator only.
        /// </summary>
        public bool IsEditable { get; }

        // ── Status after Apply ────────────────────────────────────────
        private RenameStatus _status = RenameStatus.Pending;
        public RenameStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        // ── Error detail (shown in tooltip) ──────────────────────────
        private string _errorMessage;
        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }

        // ── Constructor ───────────────────────────────────────────────
        public WorksetRowVM(WorksetId id, string name, bool isOpen, bool isEditable)
        {
            WorksetId = id;
            OriginalName = name;
            _currentName = name;
            _newName = name;
            IsOpen = isOpen;
            IsEditable = isEditable;
        }

        // ── INotifyPropertyChanged ────────────────────────────────────
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}