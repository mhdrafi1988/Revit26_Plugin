using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using Autodesk.Revit.DB;

namespace Revit22_Plugin.SectionManagerMVVMv4
{
    public class SectionPlacementViewModel : INotifyPropertyChanged
    {
        private readonly Document _doc;
        private readonly ObservableCollection<ViewSection> _allSections;
        public ICollectionView SectionsView { get; }

        private string _searchText = string.Empty;
        public string SearchText
        {
            get => _searchText;
            set
            {
                if (_searchText == value) return;
                _searchText = value;
                OnPropertyChanged();
                SectionsView.Refresh();
            }
        }

        public ObservableCollection<ViewSection> AvailableSections => _allSections;

        private int _rows = 6;
        public int Rows
        {
            get => _rows;
            set { if (_rows != value) { _rows = value; OnPropertyChanged(); } }
        }

        private int _columns = 3;
        public int Columns
        {
            get => _columns;
            set { if (_columns != value) { _columns = value; OnPropertyChanged(); } }
        }

        private double _horizontalGapMM = 300;
        public double HorizontalGapMM
        {
            get => _horizontalGapMM;
            set { if (_horizontalGapMM != value) { _horizontalGapMM = value; OnPropertyChanged(); } }
        }

        private double _verticalGapMM = 50;
        public double VerticalGapMM
        {
            get => _verticalGapMM;
            set { if (_verticalGapMM != value) { _verticalGapMM = value; OnPropertyChanged(); } }
        }

        public ObservableCollection<string> TitleBlocks { get; }
        private string _selectedTitleBlock;
        public string SelectedTitleBlock
        {
            get => _selectedTitleBlock;
            set { if (_selectedTitleBlock != value) { _selectedTitleBlock = value; OnPropertyChanged(); } }
        }

        public SectionPlacementViewModel(Document doc)
        {
            _doc = doc;
            // collect unplaced sections
            var secs = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSection))
                .Cast<ViewSection>()
                .Where(v => !v.IsTemplate
                            && !IsViewPlaced(v.Id)
                            && v.ViewType == ViewType.Section)
                .OrderBy(v => v.Name)
                .ToList();

            _allSections = new ObservableCollection<ViewSection>(secs);
            SectionsView = CollectionViewSource.GetDefaultView(_allSections);
            SectionsView.Filter = o => FilterBySearch(o as ViewSection);

            // collect title block names
            var tbNames = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(fs => fs.Category.Id.Value == (int)BuiltInCategory.OST_TitleBlocks)
                .Select(fs => fs.Name)
                .OrderBy(n => n);

            TitleBlocks = new ObservableCollection<string>(tbNames);
            _selectedTitleBlock = TitleBlocks.FirstOrDefault();
        }

        private bool FilterBySearch(ViewSection v)
        {
            if (string.IsNullOrWhiteSpace(_searchText)) return true;
            return v.Name.IndexOf(_searchText, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private bool IsViewPlaced(ElementId vid)
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(Viewport))
                .Cast<Viewport>()
                .Any(vp => vp.ViewId == vid);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
