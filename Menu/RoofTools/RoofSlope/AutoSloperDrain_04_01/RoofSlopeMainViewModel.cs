using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.Asd_V4_01.Models;
using Revit22_Plugin.Asd_V4_01.Services;
using Revit22_Plugin.Asd_V4_01.payloads;     // <-- payload v2
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;

namespace Revit22_Plugin.Asd_V4_01.ViewModels
{
    public class RoofSlopeMainViewModel : INotifyPropertyChanged
    {
        private readonly UIApplication _uiApp;
        private readonly DrainDetectionService _drainService;
        private readonly RoofSlopeProcessorService _slopeService;
        private readonly RoofBase _selectedRoof;

        public event PropertyChangedEventHandler PropertyChanged;

        private RoofData _currentRoof;
        private string _selectedSizeFilter = "All";
        private string _logText = "";

        // Commands
        public ICommand ApplySlopesCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand SelectNoneCommand { get; }
        public ICommand InvertSelectionCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ChangeRoofCommand { get; }
        public ICommand OkCommand { get; }

        // Collections
        public ObservableCollection<DrainItem> AllDrains { get; } = new ObservableCollection<DrainItem>();
        public ObservableCollection<string> SizeFilters { get; } = new ObservableCollection<string>();

        // Slope input
        private string _slopeInput = "1.5";
        public string SlopeInput
        {
            get => _slopeInput;
            set
            {
                _slopeInput = value;
                OnPropertyChanged(nameof(SlopeInput));

                if (double.TryParse(value, out double result) && result > 0)
                    AddLog($"Slope set to: {result}%");
            }
        }

        public List<string> SlopeOptions { get; } =
            new List<string> { "1.0", "1.5", "2.0", "2.5", "3.0" };

        public string SelectedSizeFilter
        {
            get => _selectedSizeFilter;
            set
            {
                _selectedSizeFilter = value;
                OnPropertyChanged(nameof(SelectedSizeFilter));
                FilterDrains();
                AddLog($"Filter applied: {value}");
            }
        }

        public string LogText
        {
            get => _logText;
            set
            {
                _logText = value;
                OnPropertyChanged(nameof(LogText));
            }
        }

        private string _roofInfo;
        public string RoofInfo
        {
            get => _roofInfo;
            set
            {
                _roofInfo = value;
                OnPropertyChanged(nameof(RoofInfo));
            }
        }

        private string _resultsInfo;
        public string ResultsInfo
        {
            get => _resultsInfo;
            set
            {
                _resultsInfo = value;
                OnPropertyChanged(nameof(ResultsInfo));
            }
        }

        private int _selectedDrainsCount;
        public int SelectedDrainsCount
        {
            get => _selectedDrainsCount;
            set
            {
                _selectedDrainsCount = value;
                OnPropertyChanged(nameof(SelectedDrainsCount));
            }
        }

        public ICollectionView FilteredDrainsView { get; }

        public Action CloseWindow { get; set; }

        // ------------------------- CONSTRUCTOR -------------------------

        public RoofSlopeMainViewModel(UIApplication uiApp, RoofBase selectedRoof)
        {
            _uiApp = uiApp;
            _selectedRoof = selectedRoof;

            _drainService = new DrainDetectionService();
            _slopeService = new RoofSlopeProcessorService();

            ApplySlopesCommand = new RelayCommand(ApplySlopes, CanApplySlopes);
            SelectAllCommand = new RelayCommand(SelectAllDrains);
            SelectNoneCommand = new RelayCommand(SelectNoneDrains);
            InvertSelectionCommand = new RelayCommand(InvertDrainSelection);
            CancelCommand = new RelayCommand(CancelOperation);
            ChangeRoofCommand = new RelayCommand(ChangeRoof);
            OkCommand = new RelayCommand(OkOperation);

            FilteredDrainsView = CollectionViewSource.GetDefaultView(AllDrains);
            FilteredDrainsView.Filter = FilterDrainItem;

            AddLog("=== Auto Roof Sloper Initialized ===");
            AddLog("All vertices reset to zero elevation");
            AddLog("Detecting inner loop openings on top surface...");

            InitializeWithRoof(selectedRoof);
        }

        // ------------------------- INITIALIZATION -------------------------

        private void InitializeWithRoof(RoofBase roof)
        {
            try
            {
                AddLog($"Selected roof: {roof.Name}");
                RoofInfo = $"Selected Roof: {roof.Name} (Id: {roof.Id})";

                _currentRoof = new RoofData { Roof = roof };

                AnalyzeRoofGeometry(_currentRoof);

                AddLog("Scanning for inner loop openings...");
                var detectedDrains = _drainService.DetectDrainsFromRoof(
                    roof,
                    _currentRoof.TopFace
                );

                _currentRoof.DetectedDrains = detectedDrains.ToList();

                AllDrains.Clear();
                foreach (var drain in detectedDrains)
                {
                    AllDrains.Add(drain);
                    AddLog($"Found opening: {drain.SizeCategory}");
                }

                SizeFilters.Clear();
                foreach (var cat in _drainService.GenerateSizeCategories(detectedDrains))
                    SizeFilters.Add(cat);

                AddLog($"✓ Completed: Found {detectedDrains.Count} openings");

                UpdateSelectedCount();
            }
            catch (Exception ex)
            {
                AddLog($"✗ ERROR during analysis: {ex.Message}");
            }
        }

        private void ChangeRoof()
        {
            AddLog("Changing roof...");
            CloseWindow?.Invoke();
        }

        private void OkOperation()
        {
            AddLog("✓ Operation complete.");
            CloseWindow?.Invoke();
        }

        private void CancelOperation()
        {
            AddLog("Operation cancelled.");
            CloseWindow?.Invoke();
        }

        private bool FilterDrainItem(object obj)
        {
            var drain = obj as DrainItem;
            if (drain == null) return false;

            if (SelectedSizeFilter == "All") return true;

            return _drainService
                .FilterDrainsBySize(new List<DrainItem> { drain }, SelectedSizeFilter)
                .Any();
        }

        private void AnalyzeRoofGeometry(RoofData roofData)
        {
            try
            {
                var roof = roofData.Roof;

                roofData.TopFace = GetTopFace(roof);

                if (roofData.TopFace == null)
                    throw new Exception("Cannot find top face of roof.");

                roofData.Vertices.Clear();
                var slabShapeEditor = roof.GetSlabShapeEditor();
                foreach (SlabShapeVertex v in slabShapeEditor.SlabShapeVertices)
                    roofData.Vertices.Add(v);

                AddLog($"Found {roofData.Vertices.Count} vertices.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Roof analysis failed: {ex.Message}");
            }
        }

        private Face GetTopFace(RoofBase roof)
        {
            GeometryElement geom = roof.get_Geometry(new Options());
            Face top = null;
            double maxZ = double.MinValue;

            foreach (GeometryObject obj in geom)
            {
                if (!(obj is Solid solid)) continue;

                foreach (Face face in solid.Faces)
                {
                    if (face == null) continue;

                    var bb = face.GetBoundingBox();
                    if (bb == null) continue;

                    UV mid = new UV((bb.Min.U + bb.Max.U) / 2, (bb.Min.V + bb.Max.V) / 2);
                    XYZ pt = face.Evaluate(mid);

                    if (pt.Z > maxZ)
                    {
                        maxZ = pt.Z;
                        top = face;
                    }
                }
            }

            return top;
        }

        // ------------------------- APPLY SLOPES -------------------------

        private void ApplySlopes()
        {
            try
            {
                if (_currentRoof == null)
                {
                    AddLog("✗ ERROR: No roof loaded.");
                    return;
                }

                if (!double.TryParse(SlopeInput, out double slope) || slope <= 0)
                {
                    AddLog("✗ Invalid slope input.");
                    return;
                }

                var selected = AllDrains.Where(d => d.IsSelected).ToList();
                if (selected.Count == 0)
                {
                    AddLog("⚠ No drains selected.");
                    return;
                }

                AddLog($"Applying {slope}% slope to {selected.Count} drains...");

                var results = _slopeService.ProcessRoofSlopes(
                    _currentRoof,
                    selected,
                    slope,
                    AddLog
                );

                ResultsInfo =
                    $"Results: {results.modifiedCount} vertices modified | " +
                    $"Max offset: {results.maxOffset:F1} mm | " +
                    $"Longest path: {results.longestPath:F2} m";

                AddLog("✓ Slope application complete.");

                // ---- Compute Highest Elevation Safely ----
                double highestElevationMeters = 0;
                try
                {
                    XYZ mid = _currentRoof.TopFace.Evaluate(new UV(0.5, 0.5));
                    highestElevationMeters = mid.Z * 0.3048;
                }
                catch
                {
                    highestElevationMeters = 0;
                }

                // ---- Payload v2 ----
                var payload = new AutoSlopePayloadv2
                {
                    SlopePercent = slope,
                    ThresholdMeters = 0,
                    DrainPoints = selected.Select(d => d.CenterPoint).ToList()
                };

                AddLog("Updating shared parameters...");

                // ---- CALL PARAMETER WRITER ----
                AutoSlopeParameterWriter.UpdateAllParameters(
                    _currentRoof.Roof.Document,
                    _currentRoof.Roof,
                    payload,
                    highestElevationMeters,
                    results.longestPath,
                    results.modifiedCount,
                    0,
                    0,
                    AddLog
                );

                AddLog("✓ Shared parameters updated.");
            }
            catch (Exception ex)
            {
                AddLog("✗ ERROR: " + ex.Message);
            }
        }

        private bool CanApplySlopes()
        {
            return _currentRoof != null && AllDrains.Any(x => x.IsSelected);
        }

        // ------------------------- SELECTION -------------------------

        private void SelectAllDrains()
        {
            foreach (var d in AllDrains) d.IsSelected = true;
            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
        }

        private void SelectNoneDrains()
        {
            foreach (var d in AllDrains) d.IsSelected = false;
            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
        }

        private void InvertDrainSelection()
        {
            foreach (var d in AllDrains) d.IsSelected = !d.IsSelected;
            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
        }

        private void FilterDrains()
        {
            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
        }

        private void UpdateSelectedCount()
        {
            SelectedDrainsCount = FilteredDrainsView
                .Cast<DrainItem>()
                .Count(x => x.IsSelected);
        }

        // ------------------------- LOGGING -------------------------

        public void AddLog(string msg)
        {
            string time = DateTime.Now.ToString("HH:mm:ss");
            LogText += $"[{time}] {msg}\n";
            OnPropertyChanged(nameof(LogText));
        }

        protected virtual void OnPropertyChanged(string prop)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }

    // ------------------------- RELAY COMMAND -------------------------

    public class RelayCommand : ICommand
    {
        private readonly Action _exec;
        private readonly Func<bool> _can;

        public RelayCommand(Action exec, Func<bool> can = null)
        {
            _exec = exec;
            _can = can;
        }

        public bool CanExecute(object p) => _can == null || _can();

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public void Execute(object p) => _exec();
    }
}
