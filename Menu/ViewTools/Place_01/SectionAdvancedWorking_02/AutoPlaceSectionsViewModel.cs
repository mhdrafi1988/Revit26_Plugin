using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
//using Revit22_Plugin.SectionManagerMVVM.Commands; // Add this using directive for RelayCommand
using Revit22_Plugin.SectionPlacer.MVVM;
using Revit22_Plugin.SectionPlacer.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Revit22_Plugin.SectionPlacer.ViewModels
{
    /// <summary>
    /// ViewModel for AutoPlaceSectionsWindow.
    /// Now includes: Horizontal/Vertical spacing and Bottom title gap/height (all in mm).
    /// </summary>
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
            set { if (_filterText == value) return; _filterText = value; OnPropertyChanged(); ApplyFilter(); }
        }

        // ---------- UI inputs (mm) ----------
        public double MarginMm { get; set; } = 20;       // sheet edge margin (min used with ISO)
        public double ReservedRightMm { get; set; } = 112;

        public double HSpacingMm { get; set; } = 20;     // user horizontal gap between sections
        public double VSpacingMm { get; set; } = 20;     // user vertical gap between sections

        public double TitleGapMm { get; set; } = 5;      // gap between annotation crop and title
        public double TitleBandMm { get; set; } = 5;     // title band height

        // ---------- Converted (feet) ----------
        public double Margin => MmToFeet(MarginMm);
        public double ReservedRight => MmToFeet(ReservedRightMm);
        public double HSpacing => MmToFeet(HSpacingMm);
        public double VSpacing => MmToFeet(VSpacingMm);
        public double TitleGap => MmToFeet(TitleGapMm);
        public double TitleBand => MmToFeet(TitleBandMm);

        // ---------- Commands ----------
        public ICommand PlaceCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand SelectNoneCommand { get; }
        public ICommand ReverseCommand { get; }

        public AutoPlaceSectionsViewModel(UIDocument uidoc)
        {
            var doc = uidoc.Document;

            // Title blocks
            TitleBlocks = new ObservableCollection<FamilySymbol>(
                new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>());

            SelectedTitleBlock = TitleBlocks.FirstOrDefault();
            if (SelectedTitleBlock == null)
                TaskDialog.Show("Error", "No title block families found in this project.");

            // Unplaced sections
            var collector = new SectionCollectorService(doc);
            _allSections = new ObservableCollection<SectionItemViewModel>(
                collector.CollectUnplacedSections().Select(v => new SectionItemViewModel(v)));
            Sections = new ObservableCollection<SectionItemViewModel>(_allSections);

            // Commands
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

        private double MmToFeet(double mm)
            => UnitUtils.ConvertToInternalUnits(mm, UnitTypeId.Millimeters);

        private void ApplyFilter()
        {
            if (string.IsNullOrWhiteSpace(FilterText))
                Sections = new ObservableCollection<SectionItemViewModel>(_allSections);
            else
            {
                var lower = FilterText.ToLower();
                Sections = new ObservableCollection<SectionItemViewModel>(
                    _allSections.Where(s => s.Name.ToLower().Contains(lower)));
            }
            OnPropertyChanged(nameof(StatusText));
        }

        public IEnumerable<ViewSection> GetSelectedSections()
            => Sections.Where(s => s.IsSelected).Select(s => s.Section);

        public string StatusText => $"{Sections.Count(s => s.IsSelected)} selected / {Sections.Count} shown";

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string prop = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }
}
