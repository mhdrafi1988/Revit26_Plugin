using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Revit22_Plugin.callout.Commands;
using Revit22_Plugin.callout.Helpers;
//using Revit22_Plugin.callout.Models;
using Revit22_Plugin.callout.Models;


namespace Revit22_Plugin.callout.ViewModels
{
    public class CalloutViewViewModel : INotifyPropertyChanged
    {

        public ObservableCollection<CalloutViewModelCall> AllViews { get; set; }
        public ObservableCollection<CalloutViewModelCall> FilteredViews { get; set; }
        public ObservableCollection<View> DraftingViews { get; set; }

        private string _sheetNameFilter;
        private string _sheetNumberFilter;
        private bool _showPlacedOnly;
        private bool _showUnplacedOnly;
        private string _calloutSize = "1000";
        private View _selectedDraftingView;


        public string SheetNameFilter
        {
            get => _sheetNameFilter;
            set { _sheetNameFilter = value; OnPropertyChanged(nameof(SheetNameFilter)); ApplyFilter(); }
        }

        public string SheetNumberFilter
        {
            get => _sheetNumberFilter;
            set { _sheetNumberFilter = value; OnPropertyChanged(nameof(SheetNumberFilter)); ApplyFilter(); }
        }

        public bool ShowPlacedOnly
        {
            get => _showPlacedOnly;
            set { _showPlacedOnly = value; OnPropertyChanged(nameof(ShowPlacedOnly)); ApplyFilter(); }
        }

        public bool ShowUnplacedOnly
        {
            get => _showUnplacedOnly;
            set { _showUnplacedOnly = value; OnPropertyChanged(nameof(ShowUnplacedOnly)); ApplyFilter(); }
        }

        public string CalloutSize
        {
            get => _calloutSize;
            set { _calloutSize = value; OnPropertyChanged(nameof(CalloutSize)); }
        }

        public View SelectedDraftingView
        {
            get => _selectedDraftingView;
            set { _selectedDraftingView = value; OnPropertyChanged(nameof(SelectedDraftingView)); }
        }

        public ICommand InsertReferenceViewsCommand { get; }

        public CalloutViewViewModel(Document doc)
        {
            AllViews = new ObservableCollection<CalloutViewModelCall>(RevitHelper.GetSectionViews(doc));
            FilteredViews = new ObservableCollection<CalloutViewModelCall>(AllViews);
            DraftingViews = new ObservableCollection<View>(RevitHelper.GetDraftingViews(doc));
            SelectedDraftingView = DraftingViews.FirstOrDefault();

            InsertReferenceViewsCommand = new RelayCommand(_ => ExecuteInsertReferences(doc));
        }

        private void ExecuteInsertReferences(Document doc)
        {
            var selectedViews = FilteredViews.Where(v => v.IsSelected).ToList();
            if (selectedViews.Count == 0 || SelectedDraftingView == null || !double.TryParse(CalloutSize, out double mm))
                return;

            double calloutSizeFt = mm / 304.8;
            CalloutViewUpdater.InsertReferences(doc, selectedViews, calloutSizeFt, SelectedDraftingView.Id);
        }

        private void ApplyFilter()
        {
            var result = AllViews.ToList();

            if (!string.IsNullOrWhiteSpace(SheetNameFilter))
                result = result.Where(v => v.SheetName?.ToLower().Contains(SheetNameFilter.ToLower()) == true).ToList();

            if (!string.IsNullOrWhiteSpace(SheetNumberFilter))
                result = result.Where(v => v.SheetNumber?.ToLower().Contains(SheetNumberFilter.ToLower()) == true).ToList();

            if (ShowPlacedOnly && !ShowUnplacedOnly)
                result = result.Where(v => v.IsPlaced).ToList();

            if (ShowUnplacedOnly && !ShowPlacedOnly)
                result = result.Where(v => !v.IsPlaced).ToList();

            FilteredViews.Clear();
            result.ForEach(FilteredViews.Add);
        }


        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
