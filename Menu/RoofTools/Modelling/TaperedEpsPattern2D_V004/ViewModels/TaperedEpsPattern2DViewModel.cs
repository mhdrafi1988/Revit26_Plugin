using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin_Revised.TaperedEpsPattern2D.V004.Core.Pattern;
using Revit26_Plugin_Revised.TaperedEpsPattern2D.V004.Events;
using Revit26_Plugin_Revised.TaperedEpsPattern2D.V004.Revit;

namespace Revit26_Plugin_Revised.TaperedEpsPattern2D.V004.ViewModels
{
    /// <summary>
    /// Main window view model. Recomputes the Core pattern live (debounced, off the UI thread)
    /// and sends Run / Reset to Revit through the external events created by the command.
    /// </summary>
    public sealed partial class TaperedEpsPattern2DViewModel : ObservableObject
    {
        private const int DebounceMs = 150;

        private readonly RunPatternHandler _runHandler;
        private readonly ExternalEvent _runEvent;
        private readonly RefreshRoofHandler _refreshHandler;
        private readonly ExternalEvent _refreshEvent;

        private RoofContext _roof;
        private int _version;
        private int _appliedVersion = -1;
        private bool _detached;

        public TaperedEpsPattern2DViewModel(
            RoofContext roof,
            IReadOnlyList<LineStyleOption> lineStyles,
            IReadOnlyList<TextNoteTypeOption> textTypes,
            RunPatternHandler runHandler,
            ExternalEvent runEvent,
            RefreshRoofHandler refreshHandler,
            ExternalEvent refreshEvent)
        {
            _roof = roof;
            _runHandler = runHandler;
            _runEvent = runEvent;
            _refreshHandler = refreshHandler;
            _refreshEvent = refreshEvent;

            Board = new BoardSettingsViewModel();
            Layers = new LayersViewModel(lineStyles, textTypes);
            Tiles = new ObservableCollection<MetricTileViewModel>
            {
                _boardsTile, _codesTile, _gridTile, _hipTile, _contourTile, _maxTile
            };

            Board.Changed += (_, _) => ScheduleRecompute();
            Layers.Changed += (_, _) => ScheduleRecompute();
            _runHandler.Completed += OnRunCompleted;
            _refreshHandler.Completed += OnRoofRefreshed;

            Roof.Update(roof);
            Board.SetOutline(roof.Outline.Outline);
            Log.Info($"Roof {roof.RoofName} in view \"{roof.ViewName}\" — {roof.Outline.ShapeName}.");
            foreach (string error in roof.Outline.Errors)
            {
                Log.Error(error);
            }

            if (lineStyles.Count == 0)
            {
                Log.Error("No line styles were found in this project.");
            }

            RecomputeNow();
        }

        // ── Header ───────────────────────────────────────────────────
        public string Title => TaperedEpsPattern2DToolInfo.Title;
        public string Version => TaperedEpsPattern2DToolInfo.Version;
        public string RevitVersion => TaperedEpsPattern2DToolInfo.RevitVersion;
        public string BuildDate => TaperedEpsPattern2DToolInfo.BuildDate;

        private readonly MetricTileViewModel _boardsTile = new MetricTileViewModel("Boards");
        private readonly MetricTileViewModel _codesTile = new MetricTileViewModel("Board Codes");
        private readonly MetricTileViewModel _gridTile = new MetricTileViewModel("Grid Lines");
        private readonly MetricTileViewModel _hipTile = new MetricTileViewModel("Hip/Ridge Lines");
        private readonly MetricTileViewModel _contourTile = new MetricTileViewModel("Contour Lines");
        private readonly MetricTileViewModel _maxTile = new MetricTileViewModel("Max Thickness");

        public ObservableCollection<MetricTileViewModel> Tiles { get; }

        // ── Body ─────────────────────────────────────────────────────
        public RoofCardViewModel Roof { get; } = new RoofCardViewModel();
        public BoardSettingsViewModel Board { get; }
        public LayersViewModel Layers { get; }
        public PreviewViewModel Preview { get; } = new PreviewViewModel();
        public ObservableCollection<SummaryRowViewModel> Summary { get; } = new ObservableCollection<SummaryRowViewModel>();
        public LogViewModel Log { get; } = new LogViewModel();

        // ── Footer ───────────────────────────────────────────────────
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(RunCommand))]
        private bool _isRunning;

        [ObservableProperty]
        private string _statusText = "Ready";

        /// <summary>Piece counts of the current preview, to compare row-start options before Run.</summary>
        [ObservableProperty]
        private string _layoutSummary = string.Empty;

        public event EventHandler? RequestClose;

        public bool CanRun =>
            !IsRunning &&
            _roof.IsValid &&
            !Board.HasErrors &&
            !Layers.HasErrors &&
            Layers.AnyLayerEnabled &&
            Preview.Result?.IsValid == true;

        [RelayCommand(CanExecute = nameof(CanRun))]
        private void Run()
        {
            if (_appliedVersion != _version)
            {
                RecomputeNow(); // never write a stale preview
            }

            Pattern2DResult? pattern = Preview.Result;
            if (pattern == null || !pattern.IsValid)
            {
                Log.Error("The pattern is not valid; fix the inputs first.");
                return;
            }

            _runHandler.Pending = new PatternWriteRequest(_roof, pattern)
            {
                LineStyles = Layers.StyleIds(),
                TextNoteTypeId = Layers.SelectedTextType?.TypeId,
                Grouping = Layers.SelectedGrouping.Value,
                ReplacePrevious = Layers.CanReplace && Layers.ReplacePrevious
            };

            ExternalEventRequest request = _runEvent.Raise();
            if (request != ExternalEventRequest.Accepted)
            {
                _runHandler.Pending = null;
                Log.Error($"Revit did not accept the request ({request}). Finish any active command in Revit and try again.");
                return;
            }

            IsRunning = true;
            StatusText = "Creating detail lines…";
            Log.Info($"Run: {pattern.Segments.Count} lines, {pattern.Labels.Count} labels requested.");
            foreach (string note in pattern.LayoutNotes)
            {
                Log.Info(note);
            }

            if (pattern.SliverCount > 0)
            {
                Log.Warn($"{pattern.SliverCount} cut piece(s) are narrower than the minimum piece size. Try another row start (Auto, B or C avoid them).");
            }

            if (pattern.ContoursOnGridLines > 0)
            {
                Log.Info($"{pattern.ContoursOnGridLines} contour piece(s) lie exactly on grid lines and are drawn once, as grid lines.");
            }
        }

        [RelayCommand]
        private void Reset()
        {
            Board.Reset();
            Layers.Reset();
            StatusText = "Settings reset";
            _refreshHandler.Current = _roof;
            if (_refreshEvent.Raise() != ExternalEventRequest.Accepted)
            {
                Log.Warn("Could not re-read the roof right now; settings were reset.");
            }
        }

        [RelayCommand]
        private void Close() => RequestClose?.Invoke(this, EventArgs.Empty);

        /// <summary>Called when the window closes: stop listening to the handlers.</summary>
        public void Detach()
        {
            _detached = true;
            _version++;
            _runHandler.Completed -= OnRunCompleted;
            _refreshHandler.Completed -= OnRoofRefreshed;
        }

        // ── Pattern computation ─────────────────────────────────────
        private Pattern2DSettings? BuildSettings()
        {
            if (!_roof.IsValid || Board.HasErrors || Layers.HasErrors)
            {
                return null;
            }

            Pattern2DSettings s = new Pattern2DSettings { MinSegmentLengthM = _roof.MinSegmentLengthMetres };
            return Layers.ApplyTo(Board.ApplyTo(s));
        }

        /// <summary>Tells the Layers card whether the contour interval is driven by the board rows, and what it is.</summary>
        private void UpdateIntervalLock()
        {
            Pattern2DSettings s = Layers.ApplyTo(Board.ApplyTo(new Pattern2DSettings()));
            Layers.IntervalLocked = s.ContourIntervalLocked;
            Layers.LockedIntervalText = s.ContourIntervalLocked
                ? string.Format(CultureInfo.CurrentCulture, "= {0:0.#} mm  ({1:0.00} m × {2:0.##} %)",
                    s.EffectiveContourIntervalMm, s.BoardWidthM, s.SlopePercent)
                : string.Empty;
        }

        private async void ScheduleRecompute()
        {
            UpdateIntervalLock();
            int version = ++_version;
            RunCommand.NotifyCanExecuteChanged();
            try
            {
                await Task.Delay(DebounceMs);
                if (version != _version || _detached)
                {
                    return;
                }

                Pattern2DSettings? settings = BuildSettings();
                Polygon2D? outline = _roof.Outline.Outline;
                if (settings == null || outline == null)
                {
                    Apply(null, version);
                    return;
                }

                Preview.IsComputing = true;
                Pattern2DResult result = await Task.Run(() => Pattern2DEngine.Generate(outline, settings));
                if (version == _version && !_detached)
                {
                    Apply(result, version);
                }
            }
            catch (Exception ex)
            {
                Log.Error("Preview failed: " + ex.Message);
            }
            finally
            {
                if (version == _version)
                {
                    Preview.IsComputing = false;
                }
            }
        }

        private void RecomputeNow()
        {
            UpdateIntervalLock();
            int version = ++_version;
            Pattern2DSettings? settings = BuildSettings();
            Polygon2D? outline = _roof.Outline.Outline;
            Apply(settings == null || outline == null ? null : Pattern2DEngine.Generate(outline, settings), version);
        }

        private void Apply(Pattern2DResult? result, int version)
        {
            _appliedVersion = version;
            Preview.Result = result;
            CultureInfo ci = CultureInfo.CurrentCulture;

            Summary.Clear();
            if (result == null || !result.IsValid)
            {
                foreach (MetricTileViewModel tile in Tiles)
                {
                    tile.Value = "—";
                }

                LayoutSummary = string.Empty;

                if (result != null)
                {
                    foreach (string error in result.Errors)
                    {
                        Log.Error(error);
                    }
                }
            }
            else
            {
                int fullCount = result.Boards.Count(b => b.Kind == PieceKind.Full);
                LayoutSummary = string.Format(ci,
                    "{0} full · {1} hip cuts · {2} straight cuts · {3} ridge row · {4} slivers",
                    fullCount,
                    result.Boards.Count(b => b.Kind == PieceKind.HipLeft || b.Kind == PieceKind.HipRight),
                    result.Boards.Count(b => b.Kind == PieceKind.Filler || b.Kind == PieceKind.Cut),
                    result.Boards.Count(b => b.Kind == PieceKind.RidgeRow),
                    result.SliverCount);
                _boardsTile.Value = string.Format(ci, "{0} ({1} full)", result.BoardCount, fullCount);
                _codesTile.Value = result.CodeCount.ToString(ci);
                _gridTile.Value = result.StatsOf(PatternLayer.Grid).LineCount.ToString(ci);
                _hipTile.Value = result.StatsOf(PatternLayer.HipRidge).LineCount.ToString(ci);
                _contourTile.Value = result.StatsOf(PatternLayer.Contour).LineCount.ToString(ci);
                _maxTile.Value = result.MaxThicknessMm.ToString("0", ci) + " mm";

                Summary.Add(Row(result, PatternLayer.Grid, "Board grid"));
                Summary.Add(Row(result, PatternLayer.HipRidge, "Hips / ridges"));
                Summary.Add(Row(result, PatternLayer.Contour, "Contours"));
                if (result.Labels.Count > 0)
                {
                    Summary.Add(new SummaryRowViewModel("Labels (text notes)", result.Labels.Count, 0));
                }
            }

            OnPropertyChanged(nameof(CanRun));
            RunCommand.NotifyCanExecuteChanged();
        }

        private static SummaryRowViewModel Row(Pattern2DResult r, PatternLayer layer, string name)
        {
            LayerStats s = r.StatsOf(layer);
            return new SummaryRowViewModel(name, s.LineCount, s.TotalLengthM);
        }

        // ── Callbacks from Revit (raised on Revit's UI thread) ───────
        private void OnRunCompleted(PatternWriteResult result)
        {
            IsRunning = false;
            if (result.Success)
            {
                StatusText = $"{result.LinesCreated} lines created";
                Log.Info(result.Message);
                if (result.ReplacedGroups > 0)
                {
                    Log.Info($"Replaced the previous pattern ({result.ReplacedGroups} group instance(s)).");
                }

                if (result.SkippedShortSegments > 0)
                {
                    Log.Warn($"{result.SkippedShortSegments} segment(s) shorter than Revit's short-curve tolerance were skipped.");
                }
            }
            else
            {
                StatusText = "Failed — see log";
                Log.Error(result.Message);
            }

            foreach (string warning in result.Warnings)
            {
                Log.Warn(warning);
            }
        }

        private void OnRoofRefreshed(RoofContext refreshed)
        {
            _roof = refreshed;
            Roof.Update(refreshed);
            Board.SetOutline(refreshed.Outline.Outline);
            Log.Info($"Roof re-read: {refreshed.Outline.ShapeName}.");
            foreach (string error in refreshed.Outline.Errors)
            {
                Log.Error(error);
            }

            RecomputeNow();
        }
    }
}
