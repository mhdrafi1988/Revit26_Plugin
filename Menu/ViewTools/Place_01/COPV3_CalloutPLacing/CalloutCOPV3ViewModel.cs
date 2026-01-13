using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

using Revit22_Plugin.copv3.Models;
using Revit22_Plugin.copv3.Services;

namespace Revit22_Plugin.copv3.ViewModels
{
    public class CalloutCOPV3ViewModel : INotifyPropertyChanged
    {
        private readonly UIApplication _uiapp;
        private readonly UIDocument _uidoc;
        private readonly Document _doc;

        public ObservableCollection<ViewSheet> Sheets { get; set; }
        public ViewSheet SelectedSheet { get; set; }

        public ObservableCollection<View> DraftingViews { get; set; }
        public View SelectedDraftingView { get; set; }

        public ObservableCollection<CalloutCOPV3Item> AllItems { get; set; }
        public ObservableCollection<CalloutCOPV3Item> FilteredItems { get; set; }

        public ObservableCollection<string> LogItems { get; private set; }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; ApplyFiltering(); OnPropertyChanged(); }
        }

        public CalloutCOPV3ViewModel(UIApplication uiapp, UIDocument uidoc)
        {
            _uiapp = uiapp;
            _uidoc = uidoc;
            _doc = uidoc.Document;

            LogItems = new ObservableCollection<string>();

            // Collect data
            Sheets = new ObservableCollection<ViewSheet>(CalloutCOPV3CollectorService.GetSheets(_doc));
            DraftingViews = new ObservableCollection<View>(CalloutCOPV3CollectorService.GetDraftingViews(_doc));

            // Default sheet = active sheet (if possible)
            var activeSheet = _uidoc.ActiveView as ViewSheet;
            SelectedSheet = Sheets.FirstOrDefault(s => s.Id == activeSheet?.Id)
                            ?? Sheets.FirstOrDefault();

            // Load items
            AllItems = new ObservableCollection<CalloutCOPV3Item>(
                CalloutCOPV3CollectorService.GetSectionItems(_doc)
            );
            FilteredItems = new ObservableCollection<CalloutCOPV3Item>(AllItems);

            // Commands
            CmdPlace = new CalloutCOPV3RelayCommand(_ => ExecutePlacement());
            CmdSelectAll = new CalloutCOPV3RelayCommand(_ => SelectAll());
            CmdSelectNone = new CalloutCOPV3RelayCommand(_ => SelectNone());
            CmdSelectInvert = new CalloutCOPV3RelayCommand(_ => InvertSelection());
        }

        private void ApplyFiltering()
        {
            FilteredItems.Clear();

            foreach (var item in AllItems)
            {
                // Sheet filter
                if (SelectedSheet != null && item.SheetId != SelectedSheet.Id)
                    continue;

                // Search
                if (!string.IsNullOrWhiteSpace(SearchText))
                {
                    string s = SearchText.ToLower();

                    bool match =
                        item.SectionName.ToLower().Contains(s) ||
                        item.SheetNumber.ToLower().Contains(s) ||
                        (item.DetailNumber ?? "").ToLower().Contains(s);

                    if (!match) continue;
                }

                FilteredItems.Add(item);
            }
        }

        private void ExecutePlacement()
        {
            if (SelectedDraftingView == null)
            {
                LogItems.Add("❌ No drafting view selected.");
                return;
            }

            var selected = FilteredItems.Where(i => i.IsSelected).ToList();
            if (!selected.Any())
            {
                LogItems.Add("⚠️ No sections selected.");
                return;
            }

            CalloutCOPV3PlacementService.PlaceCallouts(
                _doc,
                _uidoc,
                selected,
                SelectedDraftingView,
                LogItems
            );
        }

        private void SelectAll()
        {
            foreach (var i in FilteredItems) i.IsSelected = true;
        }

        private void SelectNone()
        {
            foreach (var i in FilteredItems) i.IsSelected = false;
        }

        private void InvertSelection()
        {
            foreach (var i in FilteredItems) i.IsSelected = !i.IsSelected;
        }

        public CalloutCOPV3RelayCommand CmdPlace { get; }
        public CalloutCOPV3RelayCommand CmdSelectAll { get; }
        public CalloutCOPV3RelayCommand CmdSelectNone { get; }
        public CalloutCOPV3RelayCommand CmdSelectInvert { get; }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
