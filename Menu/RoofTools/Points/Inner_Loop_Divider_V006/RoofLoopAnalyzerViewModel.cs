using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.DivideInnerLoops.V006.Models;
using Revit26_Plugin.DivideInnerLoops.V006.Services;
using Revit26_Plugin.Shared.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace Revit26_Plugin.DivideInnerLoops.V006.ViewModels
{
    /// <summary>
    /// ViewModel for Inner Loop Divider V005.
    /// Provides a global "set all" division control plus per-row editable overrides.
    /// Defaults: Circular → 6, Rectangle/Other → 4.
    /// </summary>
    public partial class RoofLoopAnalyzerViewModel : ObservableObject
    {
        private readonly Document _doc;
        private readonly RoofBase _roof;
        private readonly RoofGeometryService _geometryService;
        private readonly LoopDivisionService _divisionService;

        public ObservableCollection<RoofLoopModel> Loops { get; } = new();
        public ICollectionView LoopsView { get; private set; }
        public ObservableCollection<LogEntry> Log { get; } = new();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasLoops))]
        [NotifyPropertyChangedFor(nameof(TotalOpeningsCount))]
        private int _innerLoopCount;

        [ObservableProperty]
        private int _queuedPointCount;

        [ObservableProperty]
        private int _selectedShapeCount;

        public bool HasLoops => InnerLoopCount > 0;
        public int TotalOpeningsCount => InnerLoopCount;

        /// <summary>
        /// Global "set all" control. Pushing a value overwrites every loop's
        /// RecommendedPoints immediately. Per-row values remain editable individually.
        /// </summary>
        [ObservableProperty]
        private int _globalDivisionPoints = 6;

        partial void OnGlobalDivisionPointsChanged(int value)
        {
            foreach (var loop in Loops)
                loop.RecommendedPoints = value;

            RecomputeCounts();
            ApplyDivisionCommand.NotifyCanExecuteChanged();
        }

        public RoofLoopAnalyzerViewModel(Document doc, RoofBase roof)
        {
            _doc = doc;
            _roof = roof;
            _geometryService = new RoofGeometryService();
            _divisionService = new LoopDivisionService();
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
            Log.Clear();
            AddLog(LogLevel.Info, "Analysis started.");

            var innerLoops = _geometryService
                .ExtractCircularLoops(_roof)
                .Where(l => l.LoopType == "Inner");

            foreach (var loop in innerLoops)
            {
                loop.IsSelected = true;
                // Circular → 6, everything else → 4
                loop.RecommendedPoints = loop.LoopShapeType == "Circular" ? 6 : 4;

                loop.PropertyChanged += OnLoopPropertyChanged;
                Loops.Add(loop);
            }

            RecomputeCounts();

            int circular   = Loops.Count(l => l.LoopShapeType == "Circular");
            int rectangles = Loops.Count(l => l.LoopShapeType == "Rectangle");
            int others     = Loops.Count(l => l.LoopShapeType == "Other");

            AddLog(LogLevel.Success, $"Analysis complete — {Loops.Count} inner loop(s) found.");
            AddLog(LogLevel.Info, $"Circular: {circular}  ·  Rectangular: {rectangles}  ·  Other: {others}");
            AddLog(LogLevel.Info, "Defaults — Circular: 6 pts  ·  Rectangular/Other: 4 pts");

            ApplyDivisionCommand.NotifyCanExecuteChanged();
        }

        [RelayCommand(CanExecute = nameof(CanApply))]
        private void ApplyDivision()
        {
            var validLoops = Loops
                .Where(l => l.IsSelected && l.RecommendedPoints >= 1)
                .ToList();

            int totalPoints = validLoops.Sum(l => l.RecommendedPoints);
            AddLog(LogLevel.Info, $"Applying {totalPoints} point(s) to {validLoops.Count} loop(s)…");

            int placed = _divisionService.AddDivisionPoints(_doc, _roof, validLoops);

            if (placed >= 0)
                AddLog(LogLevel.Success, $"Done — {placed} division point(s) added to {validLoops.Count} loop(s).");
            else
                AddLog(LogLevel.Error, "Apply failed — transaction error.");
        }

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

        private bool CanApply() => Loops.Any(l => l.IsSelected && l.RecommendedPoints >= 1);

        private void SetAllSelected(bool value)
        {
            foreach (var loop in Loops)
                loop.IsSelected = value;
        }

        private void OnLoopPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RoofLoopModel.IsSelected) ||
                e.PropertyName == nameof(RoofLoopModel.RecommendedPoints))
            {
                RecomputeCounts();
                ApplyDivisionCommand.NotifyCanExecuteChanged();
            }
        }

        private void RecomputeCounts()
        {
            InnerLoopCount     = Loops.Count;
            SelectedShapeCount = Loops.Count(l => l.IsSelected);
            QueuedPointCount   = Loops.Where(l => l.IsSelected).Sum(l => l.RecommendedPoints);
        }

        private void DetachLoopHandlers()
        {
            foreach (var loop in Loops)
                loop.PropertyChanged -= OnLoopPropertyChanged;
        }

        private void AddLog(LogLevel level, string message)
            => Log.Add(new LogEntry(level, message));
    }
}
