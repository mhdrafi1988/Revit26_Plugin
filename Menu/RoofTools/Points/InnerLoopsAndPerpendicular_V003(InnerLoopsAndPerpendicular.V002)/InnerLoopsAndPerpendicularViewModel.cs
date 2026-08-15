using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.InnerLoopsAndPerpendicular.V003.Models;
using Revit26_Plugin.InnerLoopsAndPerpendicular.V003.Services;
using Revit26_Plugin.Shared.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace Revit26_Plugin.InnerLoopsAndPerpendicular.V003.ViewModels
{
    /// <summary>
    /// ViewModel for the Perpendicular Border Point Tool V001.
    /// User selects any inner loop shapes (Circular / Rectangular / Other) from the
    /// datagrid; perpendicular candidates are generated only for rectangular (4-side)
    /// shapes among the selection — non-rectangular selections are logged and skipped.
    /// </summary>
    public partial class PerpendicularPointViewModel : ObservableObject
    {
        private readonly Document _doc;
        private readonly RoofBase _roof;
        private readonly RoofGeometryService _geometryService;
        private readonly PerpendicularPointService _perpendicularService;

        private RoofLoopModel _outerLoop;

        public ObservableCollection<RoofLoopModel> Loops { get; } = new();
        public ICollectionView LoopsView { get; private set; }
        public ObservableCollection<PerpendicularPointModel> PerpendicularPoints { get; } = new();
        public ObservableCollection<LogEntry> Log { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasLoops))]
        private int _innerLoopCount;

        [ObservableProperty]
        private int _selectedShapeCount;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasPerpendicularPoints))]
        private int _perpendicularPointCount;

        public bool HasLoops => InnerLoopCount > 0;
        public bool HasPerpendicularPoints => PerpendicularPointCount > 0;

        /// <summary>
        /// Tolerance (mm) used to decide whether an existing SlabShapeVertex sits on the
        /// opening's edge (included in the point pool). User-editable, default 3mm.
        /// </summary>
        [ObservableProperty]
        private double _toleranceMm = 3.0;

        /// <summary>
        /// XY proximity (mm) used to cluster pool points into groups before casting
        /// directional perpendicular feet. User-editable, default 500mm.
        /// </summary>
        [ObservableProperty]
        private double _groupProximityMm = 500.0;

        public PerpendicularPointViewModel(Document doc, RoofBase roof)
        {
            _doc = doc;
            _roof = roof;
            _geometryService = new RoofGeometryService();
            _perpendicularService = new PerpendicularPointService();
            SetupCollectionView();
        }

        private void SetupCollectionView()
        {
            LoopsView = CollectionViewSource.GetDefaultView(Loops);
            LoopsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(RoofLoopModel.ShapeCategory)));
            LoopsView.SortDescriptions.Add(new SortDescription(nameof(RoofLoopModel.CategoryRank), ListSortDirection.Ascending));
            LoopsView.SortDescriptions.Add(new SortDescription(nameof(RoofLoopModel.PerimeterMm), ListSortDirection.Ascending));
        }

        [RelayCommand]
        private void Analyze()
        {
            DetachLoopHandlers();
            Loops.Clear();
            PerpendicularPoints.Clear();
            Log.Clear();
            AddLog(LogLevel.Info, "Analysis started.");

            var allLoops = _geometryService.ExtractCircularLoops(_roof);

            _outerLoop = allLoops.FirstOrDefault(l => l.LoopType == "Outer");

            foreach (var loop in allLoops.Where(l => l.LoopType == "Inner"))
            {
                loop.IsSelected = true;
                loop.PropertyChanged += OnLoopPropertyChanged;
                Loops.Add(loop);
            }

            RecomputeCounts();

            int circular   = Loops.Count(l => l.LoopShapeType == "Circular");
            int rectangles = Loops.Count(l => l.LoopShapeType == "Rectangle");
            int others     = Loops.Count(l => l.LoopShapeType == "Other");

            AddLog(LogLevel.Success, $"Analysis complete — {Loops.Count} shape(s) found.");
            AddLog(LogLevel.Info, $"Circular: {circular}  ·  Rectangular: {rectangles}  ·  Other: {others}");

            if (_outerLoop == null)
                AddLog(LogLevel.Warning, "No outer boundary loop found — perpendicular points unavailable.");

            GeneratePerpendicularPointsCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Generates perpendicular border-point candidates for every selected shape.
        /// Only rectangular (4-side) shapes among the selection produce valid candidates;
        /// other selected shapes are logged and skipped.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanGenerate))]
        private void GeneratePerpendicularPoints()
        {
            DetachPointHandlers();
            PerpendicularPoints.Clear();

            if (_outerLoop == null)
            {
                AddLog(LogLevel.Error, "Cannot generate perpendicular points — no outer boundary loop available.");
                RecomputePerpendicularCount();
                return;
            }

            var selectedShapes = Loops.Where(l => l.IsSelected).ToList();
            if (!selectedShapes.Any())
            {
                AddLog(LogLevel.Warning, "No shapes selected — nothing to project.");
                RecomputePerpendicularCount();
                return;
            }

            foreach (var shape in selectedShapes)
            {
                var sideCandidates = _perpendicularService.GenerateCandidates(_roof, shape, _outerLoop, ToleranceMm, GroupProximityMm);

                int validCount = sideCandidates.Count(c => c.IsValid);
                if (validCount == 0)
                {
                    AddLog(LogLevel.Warning, $"Shape #{shape.Index} ({shape.LoopShapeType}): no boundary candidates found — skipped.");
                    continue;
                }

                AddLog(LogLevel.Info, $"Shape #{shape.Index} ({shape.LoopShapeType}): {validCount} perpendicular candidate(s) found.");

                foreach (var candidate in sideCandidates)
                {
                    if (!candidate.IsValid)
                    {
                        AddLog(LogLevel.Warning, $"Shape #{candidate.ShapeIndex}: a point group had fewer than 4 directional candidates available.");
                        continue;
                    }

                    candidate.PropertyChanged += OnPerpendicularPointChanged;
                    PerpendicularPoints.Add(candidate);
                }
            }

            RecomputePerpendicularCount();
            AddLog(LogLevel.Success, $"Generated {PerpendicularPoints.Count(p => p.IsValid)} perpendicular point candidate(s).");
            ApplyPerpendicularCommand.NotifyCanExecuteChanged();
        }

        private bool CanGenerate() => Loops.Any(l => l.IsSelected);

        [RelayCommand(CanExecute = nameof(CanApplyPerpendicular))]
        private void ApplyPerpendicular()
        {
            var validPoints = PerpendicularPoints.Where(p => p.IsSelected && p.IsValid).ToList();
            AddLog(LogLevel.Info, $"Applying {validPoints.Count} perpendicular point(s)…");

            int placed = _perpendicularService.ApplyPerpendicularPoints(_doc, _roof, validPoints, out var details);

            foreach (var line in details)
                AddLog(line.Contains("rejected") ? LogLevel.Warning : LogLevel.Info, line);

            if (placed >= 0)
                AddLog(LogLevel.Success, $"Done — {placed} perpendicular point(s) added.");
            else
                AddLog(LogLevel.Error, "Apply failed — transaction error.");
        }

        private bool CanApplyPerpendicular() => PerpendicularPoints.Any(p => p.IsSelected && p.IsValid);

        [RelayCommand]
        private void SelectAll() => SetAllSelected(true);

        [RelayCommand]
        private void ClearSelection() => SetAllSelected(false);

        public void SelectGroupLoops(string category)
        {
            foreach (var loop in Loops.Where(l => l.ShapeCategory == category))
                loop.IsSelected = true;
        }

        public void ClearGroupLoops(string category)
        {
            foreach (var loop in Loops.Where(l => l.ShapeCategory == category))
                loop.IsSelected = false;
        }

        [RelayCommand]
        private void ClearLog() => Log.Clear();

        private void SetAllSelected(bool value)
        {
            foreach (var loop in Loops)
                loop.IsSelected = value;
        }

        private void OnLoopPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RoofLoopModel.IsSelected))
            {
                RecomputeCounts();
                GeneratePerpendicularPointsCommand.NotifyCanExecuteChanged();
            }
        }

        private void OnPerpendicularPointChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(PerpendicularPointModel.IsSelected))
            {
                RecomputePerpendicularCount();
                ApplyPerpendicularCommand.NotifyCanExecuteChanged();
            }
        }

        private void RecomputeCounts()
        {
            InnerLoopCount     = Loops.Count;
            SelectedShapeCount = Loops.Count(l => l.IsSelected);
        }

        private void RecomputePerpendicularCount()
            => PerpendicularPointCount = PerpendicularPoints.Count(p => p.IsSelected && p.IsValid);

        private void DetachLoopHandlers()
        {
            foreach (var loop in Loops)
                loop.PropertyChanged -= OnLoopPropertyChanged;
        }

        private void DetachPointHandlers()
        {
            foreach (var p in PerpendicularPoints)
                p.PropertyChanged -= OnPerpendicularPointChanged;
        }

        private void AddLog(LogLevel level, string message)
            => Log.Add(new LogEntry(level, message));
    }
}
