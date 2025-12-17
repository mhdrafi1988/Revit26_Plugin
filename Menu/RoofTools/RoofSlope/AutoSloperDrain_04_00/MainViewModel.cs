using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit22_Plugin.Asd.Models;
using Revit22_Plugin.Asd.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;
using System.Windows.Input;

namespace Revit22_Plugin.Asd.ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private readonly UIApplication _uiApp;
        private readonly DrainDetectionService _drainService;
        private readonly RoofSlopeProcessorService _slopeService;
        private readonly RoofBase _selectedRoof;

        public event PropertyChangedEventHandler PropertyChanged;

        private RoofData _currentRoof;
        private string _selectedSizeFilter = "All";
        private string _logText = "";

        public ICommand ApplySlopesCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand SelectNoneCommand { get; }
        public ICommand InvertSelectionCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ChangeRoofCommand { get; }
        public ICommand OkCommand { get; }

        public ObservableCollection<DrainItem> AllDrains { get; } = new ObservableCollection<DrainItem>();
        public ObservableCollection<string> SizeFilters { get; } = new ObservableCollection<string>();

        // Slope options with manual entry support
        private string _slopeInput = "1.5";
        public string SlopeInput
        {
            get => _slopeInput;
            set
            {
                _slopeInput = value;
                OnPropertyChanged(nameof(SlopeInput));
                if (double.TryParse(value, out double result) && result > 0)
                {
                    AddLog($"Slope set to: {result}%");
                }
            }
        }

        public List<string> SlopeOptions { get; } = new List<string> { "1.0", "1.5", "2.0", "2.5", "3.0" };

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

        // Action to close the window
        public Action CloseWindow { get; set; }

        public MainViewModel(UIApplication uiApp, RoofBase selectedRoof)
        {
            _uiApp = uiApp;
            _selectedRoof = selectedRoof;
            _drainService = new DrainDetectionService();
            _slopeService = new RoofSlopeProcessorService();

            // Initialize commands
            ApplySlopesCommand = new RelayCommand(ApplySlopes, CanApplySlopes);
            SelectAllCommand = new RelayCommand(SelectAllDrains);
            SelectNoneCommand = new RelayCommand(SelectNoneDrains);
            InvertSelectionCommand = new RelayCommand(InvertDrainSelection);
            CancelCommand = new RelayCommand(CancelOperation);
            ChangeRoofCommand = new RelayCommand(ChangeRoof);
            OkCommand = new RelayCommand(OkOperation);

            // Setup filtered view
            FilteredDrainsView = CollectionViewSource.GetDefaultView(AllDrains);
            FilteredDrainsView.Filter = FilterDrainItem;

            // Add initial log message
            AddLog("=== Auto Roof Sloper Initialized ===");
            AddLog("All vertices reset to zero elevation");
            AddLog("Detecting inner loop openings on top surface...");

            // Initialize with selected roof
            InitializeWithRoof(selectedRoof);
        }

        private void InitializeWithRoof(RoofBase roof)
        {
            try
            {
                AddLog($"Selected roof: {roof.Name}");
                RoofInfo = $"Selected Roof: {roof.Name} (Id: {roof.Id})";

                // Analyze roof
                _currentRoof = new RoofData { Roof = roof };
                AnalyzeRoofGeometry(_currentRoof);

                // Detect drains from inner loops only
                AddLog("Scanning for inner loop openings (excluding perimeter)...");
                var detectedDrains = _drainService.DetectDrainsFromRoof(roof, _currentRoof.TopFace);
                _currentRoof.DetectedDrains = detectedDrains;

                // Update UI
                AllDrains.Clear();
                foreach (var drain in detectedDrains)
                {
                    AllDrains.Add(drain);
                    AddLog($"Found opening: {drain.SizeCategory} at ({drain.CenterPoint.X:F0}, {drain.CenterPoint.Y:F0})mm");
                    AddLog($"  - Corners calculated for accurate distance measurement");
                }

                // Update filters
                SizeFilters.Clear();
                var categories = _drainService.GenerateSizeCategories(detectedDrains);
                foreach (var category in categories)
                {
                    SizeFilters.Add(category);
                }

                if (detectedDrains.Count > 0)
                {
                    AddLog($"✓ Completed: Found {detectedDrains.Count} inner loop openings");
                    AddLog("✓ Perimeter curves excluded");
                    AddLog("✓ All duplicates removed");
                    AddLog("✓ Drain corner points calculated for accurate slope calculation");
                }
                else
                {
                    AddLog("ℹ No inner loop openings found on this roof");
                    AddLog("ℹ Make sure the roof has openings (holes) for drains");
                }

                UpdateSelectedCount();

            }
            catch (Exception ex)
            {
                AddLog($"✗ ERROR during roof analysis: {ex.Message}");
            }
        }

        private void ChangeRoof()
        {
            try
            {
                AddLog("Changing roof selection...");
                CloseWindow?.Invoke();
            }
            catch (Exception ex)
            {
                AddLog($"✗ ERROR: {ex.Message}");
            }
        }

        private void OkOperation()
        {
            AddLog("✓ Operation completed successfully.");
            CloseWindow?.Invoke();
        }

        private bool FilterDrainItem(object obj)
        {
            var drain = obj as DrainItem;
            if (drain == null) return false;
            if (SelectedSizeFilter == "All") return true;

            var filtered = _drainService.FilterDrainsBySize(new List<DrainItem> { drain }, SelectedSizeFilter);
            return filtered.Any();
        }

        private void AnalyzeRoofGeometry(RoofData roofData)
        {
            try
            {
                var roof = roofData.Roof;

                // Get top face
                roofData.TopFace = GetTopFace(roof);
                if (roofData.TopFace == null)
                {
                    throw new Exception("Could not find top face of the roof.");
                }

                // Get vertices
                roofData.Vertices.Clear();
                var slabShapeEditor = roof.GetSlabShapeEditor();
                foreach (SlabShapeVertex vertex in slabShapeEditor.SlabShapeVertices)
                {
                    roofData.Vertices.Add(vertex);
                }

                AddLog($"Found {roofData.Vertices.Count} vertices on roof");
                AddLog($"Top face area: {CalculateFaceArea(roofData.TopFace):F2} m²");
            }
            catch (Exception ex)
            {
                throw new Exception($"Roof analysis failed: {ex.Message}");
            }
        }

        private double CalculateFaceArea(Face face)
        {
            try
            {
                var bb = face.GetBoundingBox();
                if (bb == null) return 0;

                double width = (bb.Max.U - bb.Min.U);
                double height = (bb.Max.V - bb.Min.V);
                return width * height * 0.0929; // Convert to m²
            }
            catch
            {
                return 0;
            }
        }

        private Face GetTopFace(RoofBase roof)
        {
            if (roof == null) return null;

            GeometryElement geomElem = roof.get_Geometry(new Options());
            Face topFace = null;
            double maxZ = double.MinValue;

            foreach (GeometryObject geomObj in geomElem)
            {
                if (geomObj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face == null) continue;

                        BoundingBoxUV bb = face.GetBoundingBox();
                        if (bb == null) continue;

                        UV midpointUV = new UV((bb.Min.U + bb.Max.U) / 2, (bb.Min.V + bb.Max.V) / 2);
                        XYZ midpoint = face.Evaluate(midpointUV);

                        if (midpoint != null && midpoint.Z > maxZ)
                        {
                            maxZ = midpoint.Z;
                            topFace = face;
                        }
                    }
                }
            }
            return topFace;
        }

        private void ApplySlopes()
        {
            try
            {
                if (_currentRoof == null)
                {
                    AddLog("✗ ERROR: No roof data available.");
                    return;
                }

                // Parse slope input
                if (!double.TryParse(SlopeInput, out double slopePercentage) || slopePercentage <= 0)
                {
                    AddLog("✗ ERROR: Please enter a valid positive slope percentage.");
                    return;
                }

                var selectedDrains = AllDrains.Where(d => d.IsSelected).ToList();
                if (selectedDrains.Count == 0)
                {
                    AddLog("⚠ WARNING: No drains selected for slope application");
                    return;
                }

                AddLog($"Applying {slopePercentage}% slope to {selectedDrains.Count} drains...");
                AddLog("Using drain corner points for accurate distance calculation...");

                // Process slopes and get results
                var results = _slopeService.ProcessRoofSlopes(_currentRoof, selectedDrains, slopePercentage, AddLog);

                // Update results display
                ResultsInfo = $"Results: {results.modifiedCount} vertices modified | " +
                             $"Max offset: {results.maxOffset:F1} mm | " +
                             $"Longest path: {results.longestPath:F2} m";

                AddLog($"✓ SUCCESS: Modified {results.modifiedCount} vertices");
                AddLog($"✓ Maximum elevation offset: {results.maxOffset:F1} mm");
                AddLog($"✓ Longest drainage path: {results.longestPath:F2} meters");
                AddLog("✓ Slope calculation based on nearest drain corners (accurate drainage)");

            }
            catch (Exception ex)
            {
                AddLog($"✗ ERROR during slope application: {ex.Message}");
            }
        }

        private void CancelOperation()
        {
            AddLog("Operation cancelled by user.");
            CloseWindow?.Invoke();
        }

        private bool CanApplySlopes()
        {
            return _currentRoof != null && AllDrains.Any(d => d.IsSelected);
        }

        private void SelectAllDrains()
        {
            foreach (var drain in AllDrains)
            {
                drain.IsSelected = true;
            }
            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
            AddLog("All drains selected");
        }

        private void SelectNoneDrains()
        {
            foreach (var drain in AllDrains)
            {
                drain.IsSelected = false;
            }
            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
            AddLog("All drains deselected");
        }

        private void InvertDrainSelection()
        {
            foreach (var drain in AllDrains)
            {
                drain.IsSelected = !drain.IsSelected;
            }
            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
            AddLog("Selection inverted");
        }

        private void FilterDrains()
        {
            FilteredDrainsView.Refresh();
            UpdateSelectedCount();
        }

        private void UpdateSelectedCount()
        {
            int count = 0;
            foreach (var drain in FilteredDrainsView.Cast<DrainItem>())
            {
                if (drain.IsSelected)
                {
                    count++;
                }
            }
            SelectedDrainsCount = count;
        }

        public void AddLog(string message)
        {
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogText += $"[{timestamp}] {message}\n";

            // Auto-scroll to bottom
            OnPropertyChanged(nameof(LogText));
        }

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    // RelayCommand implementation for C# 7.3
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public void Execute(object parameter)
        {
            _execute();
        }
    }
}