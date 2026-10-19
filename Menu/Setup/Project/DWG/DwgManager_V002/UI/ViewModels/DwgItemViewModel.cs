using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin_Revised.DwgManager.V002.Core.Models;

namespace Revit26_Plugin_Revised.DwgManager.V002.UI.ViewModels
{
    /// <summary>
    /// One list row: the instances of one CAD file placed in one view.
    /// </summary>
    public partial class DwgItemViewModel : ObservableObject
    {
        private readonly DwgManagerViewModel _owner;

        /// <summary>Creates a row from <paramref name="model"/>.</summary>
        public DwgItemViewModel(DwgItemModel model, DwgManagerViewModel owner)
        {
            _owner = owner;
            TypeId = model.TypeId;
            OwnerViewId = model.OwnerViewId;
            FileName = model.FileName;
            FilePath = model.FilePath;
            ViewName = model.ViewName;
            ViewCategory = model.ViewCategory;
            IsLinked = model.IsLinked;
            InstanceIds = model.InstanceIds.AsReadOnly();
            Ids = new ObservableCollection<DwgInstanceIdViewModel>(
                model.InstanceIds.Select(id => new DwgInstanceIdViewModel(id)));
            Key = MakeKey(TypeId, OwnerViewId);
        }

        /// <summary>Stable identity of the row across refreshes (type + owner view).</summary>
        public string Key { get; }

        /// <summary>The CAD link or import type.</summary>
        public ElementId TypeId { get; }

        /// <summary>Owner view, or <see cref="ElementId.InvalidElementId"/> for model-level instances.</summary>
        public ElementId OwnerViewId { get; }

        /// <summary>True when the instances belong to one view that can be opened.</summary>
        public bool HasView => OwnerViewId != ElementId.InvalidElementId;

        /// <summary>CAD file name with its real extension.</summary>
        public string FileName { get; }

        /// <summary>Full path of a linked file (tooltip), or empty.</summary>
        public string FilePath { get; }

        /// <summary>File name tooltip: the full path when known.</summary>
        public string FileToolTip => string.IsNullOrEmpty(FilePath) ? FileName : FilePath;

        /// <summary>Owner view name, or "(all views)".</summary>
        public string ViewName { get; }

        /// <summary>Second grouping level (Floor Plans, Sections, …).</summary>
        public string ViewCategory { get; }

        /// <summary>True for a CAD link, false for an import.</summary>
        public bool IsLinked { get; }

        /// <summary>First grouping level: "Linked" or "Imported".</summary>
        public string LinkGroup => IsLinked ? "Linked" : "Imported";

        /// <summary>The instances in this row.</summary>
        public IReadOnlyList<ElementId> InstanceIds { get; }

        /// <summary>Number of instances in this row.</summary>
        public int InstanceCount => InstanceIds.Count;

        /// <summary>Element-ID chips shown in the details panel.</summary>
        public ObservableCollection<DwgInstanceIdViewModel> Ids { get; }

        /// <summary>Ticked for Delete Selected / Open Views.</summary>
        [ObservableProperty] private bool isSelected;

        /// <summary>True when at least one instance is drawn in the active view.</summary>
        [ObservableProperty] private bool isActive;

        /// <summary>The element-ID panel is open.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowDetails))]
        private bool isIdsExpanded;

        /// <summary>The inline delete confirmation is open.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ShowDetails))]
        private bool isConfirmingDelete;

        /// <summary>Purge choice for this row's inline confirmation; starts from the toolbar setting.</summary>
        [ObservableProperty] private bool purgeOnConfirm;

        /// <summary>True while either details panel is open (drives DataGrid row details).</summary>
        public bool ShowDetails => IsIdsExpanded || IsConfirmingDelete;

        /// <summary>Applies the set of instance IDs drawn in the active view.</summary>
        internal void ApplyVisibility(HashSet<ElementId> visibleIds)
        {
            bool any = false;
            foreach (var chip in Ids)
            {
                chip.IsClickable = visibleIds.Contains(chip.Id);
                any |= chip.IsClickable;
            }
            IsActive = any;
        }

        /// <summary>Row key for a type + owner view pair.</summary>
        internal static string MakeKey(ElementId typeId, ElementId viewId) => $"{typeId.Value}:{viewId.Value}";

        [RelayCommand]
        private void ToggleIds() => IsIdsExpanded = !IsIdsExpanded;

        [RelayCommand]
        private void AskDelete()
        {
            PurgeOnConfirm = _owner.PurgeOnDelete;
            IsConfirmingDelete = true;
        }

        [RelayCommand]
        private void CancelDelete() => IsConfirmingDelete = false;

        [RelayCommand]
        private void ConfirmDelete() => _owner.DeleteRow(this);
    }
}
