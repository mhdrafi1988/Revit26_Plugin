using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace Revit_26.CornertoDrainArrow_V05
{
    public sealed class RoofDrainageViewModel : INotifyPropertyChanged
    {
        private readonly ILogService _log;

        // ---------- External Events ----------
        private readonly ExternalEvent _collectFamiliesEvent;
        private readonly CollectDetailFamiliesEvent _collectFamiliesHandler;

        private readonly ExternalEvent _placeDetailEvent;
        private readonly PlaceLineDetailEvent _placeDetailHandler;

        // ---------- UI Collections ----------
        public ObservableCollection<DetailFamilyOptionDto> DetailFamilies { get; } = new();

        private DetailFamilyOptionDto _selectedFamily;
        public DetailFamilyOptionDto SelectedFamily
        {
            get => _selectedFamily;
            set { _selectedFamily = value; OnPropertyChanged(); }
        }

        public ObservableCollection<LogEntryViewModel> LogEntries => _log.Entries;

        // ---------- Commands ----------
        public ICommand PlaceTestDetailCommand { get; }

        public RoofDrainageViewModel(ILogService logService)
        {
            _log = logService;

            // --- Collect families ---
            _collectFamiliesHandler = new CollectDetailFamiliesEvent();
            _collectFamiliesHandler.OnCompleted = families =>
            {
                DetailFamilies.Clear();
                foreach (var f in families)
                    DetailFamilies.Add(f);

                SelectedFamily = DetailFamilies.FirstOrDefault();
                _log.Info($"Loaded {DetailFamilies.Count} line-based detail families.");
            };
            _collectFamiliesEvent = ExternalEvent.Create(_collectFamiliesHandler);

            // --- Placement ---
            _placeDetailHandler = new PlaceLineDetailEvent();
            _placeDetailEvent = ExternalEvent.Create(_placeDetailHandler);

            PlaceTestDetailCommand = new RelayCommand(_ => PlaceTestDetail());

            // Load families immediately
            _collectFamiliesEvent.Raise();
        }

        private void PlaceTestDetail()
        {
            if (SelectedFamily == null)
            {
                _log.Log("Select a line-based detail family first.", LogEntryLevel.Warning);
                return;
            }

            // TEST GEOMETRY (visible, guaranteed)
            _placeDetailHandler.SymbolId = SelectedFamily.SymbolId;
            _placeDetailHandler.Start = new XYZ(0, 0, 0);
            _placeDetailHandler.End = new XYZ(10, 0, 0);

            _log.Info("Placing test line-based detail item...");
            _placeDetailEvent.Raise();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
