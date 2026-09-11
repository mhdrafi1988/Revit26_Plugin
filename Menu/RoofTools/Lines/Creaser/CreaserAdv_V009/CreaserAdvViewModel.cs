// ==================================
// File: CreaserAdvViewModel.cs
// Namespace: Revit26_Plugin.CreaserAdv_V008_00
// ==================================

using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.CreaserAdv.V009.Core.Models;
using Revit26_Plugin.CreaserAdv.V009.Infrastructure.ExternalEvents;
using Revit26_Plugin.CreaserAdv.V009.Services;
using Revit26_Plugin.Shared.Models;          // LogEntry, LogLevel, converters
using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;

namespace Revit26_Plugin.CreaserAdv.V009.ViewModels
{
    /// <summary>
    /// ViewModel for Creaser Advanced V008_00.
    ///
    /// Pipeline:
    ///   1. Extract crease curves  (top-face / top-face solid edges)
    ///   1b. Filter out horizontal creases (same Z on both endpoints) — always runs
    ///   2. Optionally extract boundary curves  (top-face / side-face edges)
    ///   2b. (Optional, ticked by default) Filter by Dijkstra path validity —
    ///       creases + boundary together; each point keeps exactly one edge,
    ///       its single shortest descending route to a minimum-elevation
    ///       drain node. An optional minimum-slope threshold (also toggled
    ///       here) rejects edges before path-finding. Ridge points (no valid
    ///       descent) and disconnected points (unreachable from any drain)
    ///       are excluded and counted in the run summary.
    ///   3. Project all curves to plan-view Z elevation (curve+line pairs kept
    ///      index-aligned — a dropped zero-length projection removes both)
    ///   4. (Optional) Filter creases by minimum length (curve+line pairs, index-aligned)
    ///   5. (Optional) Group creases by drain proximity, remove longest per start point
    ///   6. Place detail items along filtered lines
    ///   7. Populate <see cref="Summary"/> for the summary bar
    /// </summary>
    public partial class CreaserAdvViewModel : ObservableObject
    {
        // --------------------------------------------------
        // Fields
        // --------------------------------------------------

        private readonly Document       _doc;
        private readonly ElementId      _roofId;
        private readonly LoggingService _log;

        // --------------------------------------------------
        // UI-bound collections
        // --------------------------------------------------

        public ObservableCollection<FamilySymbol> DetailSymbols { get; }
            = new ObservableCollection<FamilySymbol>();

        /// <summary>Bound to the log ListBox. Entries come from LoggingService.</summary>
        public ObservableCollection<LogEntry> LogEntries => _log.Entries;

        // --------------------------------------------------
        // Observable properties
        // --------------------------------------------------

        [ObservableProperty]
        private FamilySymbol _selectedDetailSymbol;

        /// <summary>
        /// Drives the "Include boundary lines" checkbox.
        /// Persists within the session; false when the window first opens.
        /// </summary>
        [ObservableProperty]
        private bool _includeBoundaryLines = false;

        /// <summary>
        /// Drives the "Group by drain points" checkbox.
        /// When enabled, creases are grouped by proximity and longest per start point is removed.
        /// </summary>
        [ObservableProperty]
        private bool _enableDrainGrouping = true;

        /// <summary>
        /// Drives the "Filter by Dijkstra path validity" checkbox (ticked by default).
        /// When enabled, every crease/boundary segment must genuinely descend toward
        /// some minimum-elevation drain node. Each non-drain point keeps exactly
        /// one outgoing edge — its single shortest descending route. Ridge points
        /// (no valid descent) and disconnected points (unreachable from any drain)
        /// are excluded and counted in the run summary.
        /// Runs on creases + boundary curves together, right after the horizontal filter,
        /// before minimum-length and drain-proximity grouping.
        /// </summary>
        [ObservableProperty]
        private bool _enableDijkstraPathFilter = true;

        /// <summary>
        /// Drives the "Enforce minimum slope" checkbox in the Minimum Slope card.
        /// Only meaningful (and only shown enabled in the UI) when
        /// <see cref="EnableDijkstraPathFilter"/> is also on — the slope check
        /// runs inside the same Dijkstra graph-build step.
        /// </summary>
        [ObservableProperty]
        private bool _enableMinimumSlope = true;

        /// <summary>
        /// Minimum required slope as a percentage (ΔZ / horizontal 2D length × 100).
        /// Edges below this are rejected before path-finding. Default 1.5%.
        /// </summary>
        [ObservableProperty]
        private double _minimumSlopePercent = 1.5;

        /// <summary>
        /// Proximity radius for drain point grouping in millimeters (default 500mm).
        /// </summary>
        [ObservableProperty]
        private double _drainGroupingRadiusMm = 500.0;

        /// <summary>
        /// Drives the "Drop lines less than minimum" checkbox.
        /// When enabled, crease lines shorter than threshold are removed (applies to creases only).
        /// </summary>
        [ObservableProperty]
        private bool _enableMinimumLength = true;

        /// <summary>
        /// Minimum line length threshold in millimeters (default 500mm).
        /// Only applies to crease lines, not boundary lines.
        /// </summary>
        [ObservableProperty]
        private double _minimumLengthMm = 500.0;

        /// <summary>Populated after every Run. Null before the first run.</summary>
        [ObservableProperty]
        private RunSummary _summary;

        /// <summary>Controls summary bar visibility — false until first Run.</summary>
        [ObservableProperty]
        private bool _hasSummary = false;

        // --------------------------------------------------
        // Constructor
        // --------------------------------------------------

        public CreaserAdvViewModel(
            UIApplication  uiApp,
            Element        roof,
            LoggingService log)
        {
            if (uiApp == null) throw new ArgumentNullException(nameof(uiApp));

            _doc    = uiApp.ActiveUIDocument.Document;
            _roofId = (roof ?? throw new ArgumentNullException(nameof(roof))).Id;
            _log    = log  ?? throw new ArgumentNullException(nameof(log));

            LoadDetailSymbols();

            CreaserAdvEventManager.Init();
        }

        // --------------------------------------------------
        // Load detail symbols
        // --------------------------------------------------

        private void LoadDetailSymbols()
        {
            DetailSymbols.Clear();

            foreach (FamilySymbol s in new DetailItemCollectorService(_doc, _log).Collect())
                DetailSymbols.Add(s);

            if (DetailSymbols.Count > 0)
                SelectedDetailSymbol = DetailSymbols[0];
        }

        // --------------------------------------------------
        // Run command (via ExternalEvent)
        // --------------------------------------------------
        // A modeless window cannot call the Revit API directly from a button
        // click — the pipeline that used to run here directly now lives in
        // CreaserAdvEngine, invoked through CreaserAdvHandler/EventManager,
        // matching the convention already used by InnerLoopDivider/
        // InnerLoopsAndPerpendicular/OuterCurveDivider/AutoSlopeByDrain.

        /// <summary>Raised after Run completes or bails out early (true = success),
        /// so callers outside this ViewModel (e.g. the Combined Roof Tools
        /// "Run All" orchestrator) can await completion without polling.</summary>
        public event Action<bool> RunCompleted;

        [RelayCommand]
        private void Run()
        {
            if (SelectedDetailSymbol == null)
            {
                _log.Warning("Please select a detail item.");
                RunCompleted?.Invoke(false);
                return;
            }

            CreaserAdvHandler.Payload = new CreaserAdvPayload
            {
                RoofId = _roofId,
                SelectedDetailSymbolId = SelectedDetailSymbol.Id,
                IncludeBoundaryLines = IncludeBoundaryLines,
                EnableDrainGrouping = EnableDrainGrouping,
                EnableDijkstraPathFilter = EnableDijkstraPathFilter,
                EnableMinimumSlope = EnableMinimumSlope,
                MinimumSlopePercent = MinimumSlopePercent,
                DrainGroupingRadiusMm = DrainGroupingRadiusMm,
                EnableMinimumLength = EnableMinimumLength,
                MinimumLengthMm = MinimumLengthMm,
                Log = _log,
                OnCompleted = result =>
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!result.Success)
                        {
                            _log.Error($"Run failed: {result.ErrorMessage}");
                            RunCompleted?.Invoke(false);
                            return;
                        }

                        UpdateSummary(result.CreasesFound, result.BoundaryFound, result.Created, result.Failed,
                            result.RidgePoints, result.DisconnectedPoints);
                        RunCompleted?.Invoke(true);
                    }));
                }
            };

            CreaserAdvEventManager.Event.Raise();
        }

        // --------------------------------------------------
        // Clear log command
        // --------------------------------------------------

        [RelayCommand]
        private void ClearLog() => _log.Clear();

        // --------------------------------------------------
        // Copy log command
        // --------------------------------------------------

        [RelayCommand]
        private void CopyLog()
        {
            if (_log.Entries.Count == 0) return;

            var sb = new StringBuilder();
            foreach (LogEntry entry in _log.Entries)
                sb.AppendLine(entry.ToString());

            Clipboard.SetText(sb.ToString());
            _log.Info("Log copied to clipboard.");
        }

        // --------------------------------------------------
        // Private helpers
        // --------------------------------------------------

        private void UpdateSummary(int creasesFound, int boundaryFound, int created, int failed, int ridgePoints = 0, int disconnectedPoints = 0)
        {
            Summary    = new RunSummary(creasesFound, boundaryFound, created, failed, ridgePoints, disconnectedPoints);
            HasSummary = true;
        }
    }
}
