using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.AnnotationOverlapDetection.V004.Models;

namespace Revit26_Plugin.AnnotationOverlapDetection.V004.ViewModels
{
    /// <summary>
    /// View model for the batch picker (v4.2.0). The user ticks sheets and/or views;
    /// the check covers every ticked view plus every supported view placed on a ticked sheet.
    /// Pure data - the lists are filled by the caller in a Revit API context.
    /// </summary>
    public partial class BatchScopePickerViewModel : ObservableObject
    {
        /// <summary>All sheets that have at least one supported view placed on them.</summary>
        public ObservableCollection<ScopeItem> Sheets { get; }

        /// <summary>All supported, non-template views.</summary>
        public ObservableCollection<ScopeItem> Views { get; }

        /// <summary>Filtered view of <see cref="Sheets"/>.</summary>
        public ICollectionView SheetsView { get; }

        /// <summary>Filtered view of <see cref="Views"/>.</summary>
        public ICollectionView ViewsView { get; }

        [ObservableProperty]
        private string filterText = string.Empty;

        [ObservableProperty]
        private string selectionSummary = "Nothing selected";

        /// <summary>Set by the window: true when the user pressed Check.</summary>
        public bool Accepted { get; set; }

        /// <summary>Raised to close the window.</summary>
        public event Action RequestClose;

        /// <summary>Creates the picker with pre-built rows.</summary>
        public BatchScopePickerViewModel(IEnumerable<ScopeItem> sheets, IEnumerable<ScopeItem> views)
        {
            Sheets = new ObservableCollection<ScopeItem>(sheets);
            Views = new ObservableCollection<ScopeItem>(views);

            foreach (var item in Sheets.Concat(Views))
                item.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(ScopeItem.IsSelected)) UpdateSummary(); };

            SheetsView = new CollectionViewSource { Source = Sheets }.View;
            ViewsView = new CollectionViewSource { Source = Views }.View;
            SheetsView.Filter = Matches;
            ViewsView.Filter = Matches;
        }

        /// <summary>The distinct view ids to check: ticked views plus views on ticked sheets.</summary>
        public HashSet<long> SelectedViewIds()
        {
            var ids = new HashSet<long>(Views.Where(v => v.IsSelected).Select(v => v.Id));
            foreach (var sheet in Sheets.Where(s => s.IsSelected))
                ids.UnionWith(sheet.ViewIds);
            return ids;
        }

        partial void OnFilterTextChanged(string value)
        {
            SheetsView.Refresh();
            ViewsView.Refresh();
        }

        private bool Matches(object o)
        {
            if (string.IsNullOrWhiteSpace(FilterText) || o is not ScopeItem item)
                return true;
            return (item.Name ?? string.Empty).Contains(FilterText.Trim(), StringComparison.OrdinalIgnoreCase)
                || (item.Detail ?? string.Empty).Contains(FilterText.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateSummary()
        {
            int sheets = Sheets.Count(s => s.IsSelected);
            int views = Views.Count(v => v.IsSelected);
            int total = SelectedViewIds().Count;
            SelectionSummary = total == 0
                ? "Nothing selected"
                : $"{sheets} sheet(s), {views} view(s) ticked → {total} view(s) will be checked";
        }

        /// <summary>Ticks every row currently visible (after filtering) in both tabs.</summary>
        [RelayCommand]
        private void SelectVisible()
        {
            foreach (ScopeItem i in SheetsView) i.IsSelected = true;
            foreach (ScopeItem i in ViewsView) i.IsSelected = true;
        }

        /// <summary>Unticks everything.</summary>
        [RelayCommand]
        private void ClearAll()
        {
            foreach (var i in Sheets.Concat(Views)) i.IsSelected = false;
        }

        /// <summary>Accepts the selection (needs at least one view).</summary>
        [RelayCommand]
        private void Accept()
        {
            if (SelectedViewIds().Count == 0)
            {
                SelectionSummary = "Tick at least one sheet or view.";
                return;
            }
            Accepted = true;
            RequestClose?.Invoke();
        }

        /// <summary>Closes without changing the scope.</summary>
        [RelayCommand]
        private void Cancel()
        {
            Accepted = false;
            RequestClose?.Invoke();
        }
    }
}
