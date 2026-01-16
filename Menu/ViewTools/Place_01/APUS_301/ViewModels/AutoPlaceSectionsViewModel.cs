using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.APUS_301.MVVM;
using Revit26_Plugin.APUS_301.Services;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Collections.Generic;

namespace Revit26_Plugin.APUS_301.ViewModels
{
    public class AutoPlaceSectionsViewModel : INotifyPropertyChanged
    {
        // ---------- Data ----------
        public ObservableCollection<FamilySymbol> TitleBlocks { get; }
        public FamilySymbol SelectedTitleBlock { get; set; }

        private ObservableCollection<SectionItemViewModel> _allSections;
        private ObservableCollection<SectionItemViewModel> _sections;
        public ObservableCollection<SectionItemViewModel> Sections
        {
            get => _sections;
            private set { _sections = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); }
        }

        private string _filterText;
        public string FilterText
        {
            get => _filterText;
            set
            {
                if (_filterText == value) return;
                _filterText = value;
                ApplyFilter();
                OnPropertyChanged();
            }
        }

        // ---------- UI OPTIONS (mm) ----------
        public double MarginMm { get; set; } = 20;
        public double ReservedRightMm { get; set; } = 112;
        public double HSpacingMm { get; set; } = 20;
        public double VSpacingMm { get; set; } = 20;
        public double TitleGapMm { get; set; } = 5;
        public double TitleBandMm { get; set; } = 5;

        // ---------- Commands ----------
        public ICommand PlaceCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand SelectNoneCommand { get; }
        public ICommand ReverseCommand { get; }

        public AutoPlaceSectionsViewModel(UIDocument uidoc)
        {
            Document doc = uidoc.Document;

            TitleBlocks = new ObservableCollection<FamilySymbol>(
                new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_TitleBlocks)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>());

            SelectedTitleBlock = TitleBlocks.FirstOrDefault();

            var collector = new SectionCollectorService(doc);
            _allSections = new ObservableCollection<SectionItemViewModel>(
                collector.CollectUnplacedSections().Select(v => new SectionItemViewModel(v)));

            Sections = new ObservableCollection<SectionItemViewModel>(_allSections);

            PlaceCommand = new RelayCommand(_ =>
            {
                SectionPlacerEventManager.PlaceHandler.Payload = this;
                SectionPlacerEventManager.PlaceEvent.Raise();
            });

            SelectAllCommand = new RelayCommand(_ =>
            {
                foreach (var s in Sections) s.IsSelected = true;
                OnPropertyChanged(nameof(StatusText));
            });

            SelectNoneCommand = new RelayCommand(_ =>
            {
                foreach (var s in Sections) s.IsSelected = false;
                OnPropertyChanged(nameof(StatusText));
            });

            ReverseCommand = new RelayCommand(_ =>
            {
                foreach (var s in Sections) s.IsSelected = !s.IsSelected;
                OnPropertyChanged(nameof(StatusText));
            });
        }

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(FilterText))
            {
                Sections = new ObservableCollection<SectionItemViewModel>(_allSections);
                return;
            }

            string lower = FilterText.ToLower();
            Sections = new ObservableCollection<SectionItemViewModel>(
                _allSections.Where(s => s.Name.ToLower().Contains(lower)));
        }

        public IEnumerable<ViewSection> GetSelectedSections()
            => Sections.Where(s => s.IsSelected).Select(s => s.Section);

        public string StatusText =>
            $"{Sections.Count(s => s.IsSelected)} selected / {Sections.Count} shown";

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    internal class RelayCommand : ICommand
    {
        private readonly System.Action<object> _execute;
        public RelayCommand(System.Action<object> execute) => _execute = execute;
        public bool CanExecute(object parameter) => true;
        public void Execute(object parameter) => _execute(parameter);
        public event System.EventHandler CanExecuteChanged;
    }
}
