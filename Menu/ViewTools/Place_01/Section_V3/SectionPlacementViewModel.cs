using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;

namespace Revit22_Plugin.SectionPlacement
{
    public class SectionPlacementViewModel : ObservableObject
    {
        private Document _doc;

        public ObservableCollection<FamilySymbol> TitleBlocks { get; }
        public ObservableCollection<View> ViewTemplates { get; }
        public ObservableCollection<ViewSheet> ExistingSheets { get; }

        private FamilySymbol _selectedTitleBlock;
        public FamilySymbol SelectedTitleBlock
        {
            get => _selectedTitleBlock;
            set => SetProperty(ref _selectedTitleBlock, value);
        }

        private View _selectedViewTemplate;
        public View SelectedViewTemplate
        {
            get => _selectedViewTemplate;
            set => SetProperty(ref _selectedViewTemplate, value);
        }

        private ObservableCollection<ViewSheet> _selectedSheets = new ObservableCollection<ViewSheet>();
        public ObservableCollection<ViewSheet> SelectedSheets
        {
            get => _selectedSheets;
            set => SetProperty(ref _selectedSheets, value);
        }

        private int _rows = 3;
        public int Rows
        {
            get => _rows;
            set => SetProperty(ref _rows, value);
        }

        private int _columns = 4;
        public int Columns
        {
            get => _columns;
            set => SetProperty(ref _columns, value);
        }

        private double _xGap = 0.25; // feet
        public double XGap
        {
            get => _xGap;
            set => SetProperty(ref _xGap, value);
        }

        private double _yGap = 0.25; // feet
        public double YGap
        {
            get => _yGap;
            set => SetProperty(ref _yGap, value);
        }

        private string _previewText;
        public string PreviewText
        {
            get => _previewText;
            set => SetProperty(ref _previewText, value);
        }

        private bool _autoCreateNewSheets = true;
        public bool AutoCreateNewSheets
        {
            get => _autoCreateNewSheets;
            set => SetProperty(ref _autoCreateNewSheets, value);
        }

        public RelayCommand PreviewCommand { get; }
        public RelayCommand OkCommand { get; }

        private readonly int _totalSections;

        public SectionPlacementViewModel(Document doc, System.Collections.Generic.List<ViewSection> sections)
        {
            _doc = doc;
            _totalSections = sections.Count;

            TitleBlocks = new ObservableCollection<FamilySymbol>(
                new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
            );

            ViewTemplates = new ObservableCollection<View>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Where(v => v.IsTemplate)
            );

            ExistingSheets = new ObservableCollection<ViewSheet>(
                new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSheet))
                    .Cast<ViewSheet>()
                    .OrderBy(s => s.SheetNumber)
            );

            SelectedTitleBlock = TitleBlocks.FirstOrDefault();
            SelectedViewTemplate = ViewTemplates.FirstOrDefault();

            PreviewCommand = new RelayCommand(UpdatePreview);
        }

        private void UpdatePreview()
        {
            int capacity = Rows * Columns;
            int sheets = (int)System.Math.Ceiling((double)_totalSections / capacity);

            PreviewText = $"{_totalSections} sections will require about {sheets} sheet(s) " +
                          $"with a {Rows}x{Columns} grid.";
        }
    }
}
