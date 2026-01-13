using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.copv2.Helpers;
using Revit22_Plugin.copv2.Models;
using Revit22_Plugin.Relay;

namespace Revit22_Plugin.copv2.ViewModels
{
    public class CalloutViewViewModel : INotifyPropertyChanged
    {
        // --------------------------------------------------------------------
        // Fields
        // --------------------------------------------------------------------
        private readonly UIDocument _uidoc;
        private readonly Document _doc;

        public ObservableCollection<CalloutViewModelCall> AllViews { get; set; }
        public ObservableCollection<CalloutViewModelCall> FilteredViews { get; set; }

        public ObservableCollection<View> DraftingViews { get; set; }
        public View SelectedDraftingView { get; set; }

        public ObservableCollection<ViewSheet> Sheets { get; set; }
        public ViewSheet SelectedSheet { get; set; }

        private string _searchText = "";
        private string _calloutSize = "1000"; // mm default

        // --------------------------------------------------------------------
        // Properties
        // --------------------------------------------------------------------
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                ApplyFilters();
            }
        }

        public string CalloutSize
        {
            get => _calloutSize;
            set
            {
                _calloutSize = value;
                OnPropertyChanged();
            }
        }

        // --------------------------------------------------------------------
        // Commands
        // --------------------------------------------------------------------
        public ICommand InsertCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand SelectNoneCommand { get; }
        public ICommand InvertSelectionCommand { get; }

        // --------------------------------------------------------------------
        // Constructor
        // --------------------------------------------------------------------
        public CalloutViewViewModel(UIDocument uidoc, Document doc)
        {
            _uidoc = uidoc;
            _doc = doc;

            // 1) Load all section views
            AllViews = new ObservableCollection<CalloutViewModelCall>(RevitHelper.GetSectionViews(doc));
            FilteredViews = new ObservableCollection<CalloutViewModelCall>(AllViews);

            // 2) Load drafting views
            DraftingViews = new ObservableCollection<View>(RevitHelper.GetDraftingViews(doc));
            SelectedDraftingView = DraftingViews.FirstOrDefault();

            // 3) Load sheets, set default sheet to active if possible
            Sheets = new ObservableCollection<ViewSheet>(RevitHelper.GetAllSheets(doc));
            var activeSheetId = RevitHelper.GetActiveSheetId(doc, uidoc);
            SelectedSheet = Sheets.FirstOrDefault(s => s.Id == activeSheetId) ?? Sheets.FirstOrDefault();

            // Instant filter when sheet changes
            PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SelectedSheet))
                    ApplyFilters();
            };

            // Commands
            InsertCommand = new RelayCommand(_ => ExecuteInsert());
            SelectAllCommand = new RelayCommand(_ => SelectAll());
            SelectNoneCommand = new RelayCommand(_ => SelectNone());
            InvertSelectionCommand = new RelayCommand(_ => InvertSelection());
        }

        // --------------------------------------------------------------------
        // Filter Logic
        // --------------------------------------------------------------------
        private void ApplyFilters()
        {
            FilteredViews.Clear();

            foreach (var item in AllViews)
            {
                bool sheetMatch = true;
                bool searchMatch = true;

                // Sheet filter (OPTION B: strict)
                if (SelectedSheet != null)
                {
                    if (!item.IsPlaced || item.SheetId != SelectedSheet.Id)
                        sheetMatch = false;
                }

                // Search text filter
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    string search = SearchText.ToLower();
                    if (!(item.SectionName.ToLower().Contains(search) ||
                          item.SheetNumber?.ToLower().Contains(search) == true ||
                          item.DetailNumber?.ToLower().Contains(search) == true))
                    {
                        searchMatch = false;
                    }
                }

                item.IsVisible = sheetMatch && searchMatch;

                if (item.IsVisible)
                    FilteredViews.Add(item);
            }
        }

        // --------------------------------------------------------------------
        // Selection Commands
        // --------------------------------------------------------------------
        private void SelectAll()
        {
            foreach (var item in FilteredViews)
                item.IsSelected = true;

            OnPropertyChanged(nameof(FilteredViews));
        }

        private void SelectNone()
        {
            foreach (var item in FilteredViews)
                item.IsSelected = false;

            OnPropertyChanged(nameof(FilteredViews));
        }

        private void InvertSelection()
        {
            foreach (var item in FilteredViews)
                item.IsSelected = !item.IsSelected;

            OnPropertyChanged(nameof(FilteredViews));
        }

        // --------------------------------------------------------------------
        // Execution of Callout Placement
        // --------------------------------------------------------------------
        private void ExecuteInsert()
        {
            var sheet = SelectedSheet;
            if (sheet == null)
            {
                TaskDialog.Show("Error", "Please select a sheet.");
                return;
            }

            if (SelectedDraftingView == null)
            {
                TaskDialog.Show("Error", "Please select a drafting view.");
                return;
            }

            var selectedViews = FilteredViews.Where(v => v.IsSelected && v.IsVisible).ToList();
            if (selectedViews.Count == 0)
            {
                TaskDialog.Show("Error", "No section views selected.");
                return;
            }

            if (!double.TryParse(CalloutSize, out double mm))
            {
                TaskDialog.Show("Error", "Invalid callout size.");
                return;
            }

            double sizeFt = UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);

            CalloutViewUpdater.InsertReferences(
                _doc,
                _uidoc,
                selectedViews,
                sizeFt,
                SelectedDraftingView.Id,
                sheet.Id);
        }

        // --------------------------------------------------------------------
        // INotifyPropertyChanged
        // --------------------------------------------------------------------
        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
