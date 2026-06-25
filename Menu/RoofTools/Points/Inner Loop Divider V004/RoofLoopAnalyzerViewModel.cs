using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.DivideInnerLoops.V004.Models;
using Revit26_Plugin.DivideInnerLoops.V004.Services;
using Revit26_Plugin.Shared.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Data;

namespace Revit26_Plugin.DivideInnerLoops.V004.ViewModels
{
    /// <summary>
    /// View-model for the Inner Loop Divider window. Extracts the roof's inner
    /// boundary loops, lets the user choose which loops to divide and how many
    /// division points each receives, and applies the result to the model.
    /// Supports hierarchical grouping (Circular, Rectangular, Other) with
    /// per-group selection controls and a live summary of selected shapes and points.
    /// </summary>
    public partial class RoofLoopAnalyzerViewModel : ObservableObject
    {
        private readonly Document _doc;
        private readonly RoofBase _roof;
        private readonly RoofGeometryService _geometryService;
        private readonly LoopDivisionService _divisionService;

        /// <summary>Inner loops discovered on the roof, bound to the data grid.</summary>
        public ObservableCollection<RoofLoopModel> Loops { get; } = new();

        /// <summary>CollectionViewSource for hierarchical grouping by shape category.</summary>
        public ICollectionView LoopsView { get; private set; }

        /// <summary>Timestamped activity entries shown in the log panel.</summary>
        public ObservableCollection<LogEntry> Log { get; } = new();

        /// <summary>Total number of inner loops available.</summary>
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HasLoops))]
        [NotifyPropertyChangedFor(nameof(TotalOpeningsCount))]
        private int _innerLoopCount;

        /// <summary>Total division points queued across all selected loops.</summary>
        [ObservableProperty]
        private int _queuedPointCount;

        /// <summary>Number of currently selected loops.</summary>
        [ObservableProperty]
        private int _selectedShapeCount;

        /// <summary>True when at least one inner loop is available; drives grid vs. empty-state visibility.</summary>
        public bool HasLoops => InnerLoopCount > 0;

        /// <summary>Total count of all openings (same as <see cref="InnerLoopCount"/>).</summary>
        public int TotalOpeningsCount => InnerLoopCount;

        /// <summary>
        /// Creates the view-model for a given roof. Analysis is not run here;
        /// the hosting command invokes <see cref="AnalyzeCommand"/> once the
        /// window is wired up.
        /// </summary>
        /// <param name="doc">The active Revit document.</param>
        /// <param name="roof">The roof whose inner loops will be analysed.</param>
        public RoofLoopAnalyzerViewModel(Document doc, RoofBase roof)
        {
            _doc = doc;
            _roof = roof;
            _geometryService = new RoofGeometryService();
            _divisionService = new LoopDivisionService();

            // Setup collection view with grouping
            SetupCollectionView();
        }

        /// <summary>Sets up the collection view with grouping by <see cref="RoofLoopModel.ShapeCategory"/>.</summary>
        private void SetupCollectionView()
        {
            LoopsView = CollectionViewSource.GetDefaultView(Loops);
            LoopsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(RoofLoopModel.ShapeCategory)));
            LoopsView.SortDescriptions.Add(new SortDescription(nameof(RoofLoopModel.CategoryRank), ListSortDirection.Ascending));
            LoopsView.SortDescriptions.Add(new SortDescription(nameof(RoofLoopModel.PerimeterMm), ListSortDirection.Ascending));
        }

        /// <summary>
        /// Re-extracts inner loops from the roof, applies default division
        /// recommendations, refreshes counts, and logs a breakdown.
        /// </summary>
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
                // Circular loops get a sensible default; others start at zero
                // and require an explicit value from the user before applying.
                loop.IsSelected = true;
                loop.RecommendedPoints = loop.LoopShapeType == "Circular" ? 3 : 0;

                loop.PropertyChanged += OnLoopPropertyChanged;
                Loops.Add(loop);
            }

            RecomputeCounts();

            int circular   = Loops.Count(l => l.LoopShapeType == "Circular");
            int rectangles = Loops.Count(l => l.LoopShapeType == "Rectangle");
            int others     = Loops.Count(l => l.LoopShapeType == "Other");

            AddLog(LogLevel.Success, $"Roof analysis complete — {Loops.Count} inner loops.");
            AddLog(LogLevel.Info, $"Circular: {circular} · Rectangular: {rectangles} · Other: {others}");

            int zeroPoint = Loops.Count(l => l.RecommendedPoints == 0);
            if (zeroPoint > 0)
                AddLog(LogLevel.Warning, $"{zeroPoint} loop(s) have 0 division points and will be skipped.");

            ApplyDivisionCommand.NotifyCanExecuteChanged();
        }

        /// <summary>
        /// Applies division points to every selected loop that has at least one
        /// point queued. The underlying service performs the Revit write inside
        /// its own named transaction.
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanApply))]
        private void ApplyDivision()
        {
            var validLoops = Loops
                .Where(l => l.IsSelected && l.RecommendedPoints >= 1)
                .ToList();

            int totalPoints = validLoops.Sum(l => l.RecommendedPoints);
            AddLog(LogLevel.Info, $"Adding {totalPoints} division point(s) to {validLoops.Count} loop(s)…");

            _divisionService.AddDivisionPoints(_doc, _roof, validLoops);

            AddLog(LogLevel.Success, $"Division points applied — {totalPoints} point(s) added.");
        }

        /// <summary>Selects every loop in the list.</summary>
        [RelayCommand]
        private void SelectAll() => SetAllSelected(true);

        /// <summary>Clears the selection on every loop.</summary>
        [RelayCommand]
        private void ClearSelection() => SetAllSelected(false);

        /// <summary>Selects all loops in a given shape category.</summary>
        /// <param name="category">The shape category: "Circular", "Rectangular", or "Other".</param>
        public void SelectGroupLoops(string category)
        {
            foreach (var loop in Loops.Where(l => l.ShapeCategory == category))
                loop.IsSelected = true;
        }

        /// <summary>Clears selection on all loops in a given shape category.</summary>
        /// <param name="category">The shape category: "Circular", "Rectangular", or "Other".</param>
        public void ClearGroupLoops(string category)
        {
            foreach (var loop in Loops.Where(l => l.ShapeCategory == category))
                loop.IsSelected = false;
        }

        /// <summary>Empties the activity log.</summary>
        [RelayCommand]
        private void ClearLog() => Log.Clear();

        /// <summary>True when any selected loop has at least one division point queued.</summary>
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
            InnerLoopCount    = Loops.Count;
            SelectedShapeCount = Loops.Count(l => l.IsSelected);
            QueuedPointCount  = Loops.Where(l => l.IsSelected).Sum(l => l.RecommendedPoints);
        }

        private void DetachLoopHandlers()
        {
            foreach (var loop in Loops)
                loop.PropertyChanged -= OnLoopPropertyChanged;
        }

        private void AddLog(LogLevel level, string message) => Log.Add(new LogEntry(level, message));
    }
}
