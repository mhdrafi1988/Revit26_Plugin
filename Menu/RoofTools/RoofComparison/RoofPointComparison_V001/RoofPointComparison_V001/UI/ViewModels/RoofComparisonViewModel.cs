using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.RoofPointComparison.V001.Core.Models;
using Revit26_Plugin.RoofPointComparison.V001.Infrastructure.ExternalEvents;
using Revit26_Plugin.RoofPointComparison.V001.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;

namespace Revit26_Plugin.RoofPointComparison.V001.UI.ViewModels
{
    public partial class RoofComparisonViewModel : ObservableObject
    {
        public UIDocument UIDoc { get; }
        public UIApplication App { get; }
        public ElementId RoofAId { get; }
        public ElementId RoofBId { get; }

        // ── Tolerances ────────────────────────────────────────────────────────
        [ObservableProperty]
        private double positionToleranceMm = 10;

        [ObservableProperty]
        private double elevationToleranceMm = 1;

        // ── Marker styles ─────────────────────────────────────────────────────
        public MarkerStyleGroup MissingMarkerGroup { get; } = new MarkerStyleGroup
        {
            GroupLabel = "Missing Point",
            ColorName = "Red",
            RadiusMm = 150
        };

        public MarkerStyleGroup MismatchMarkerGroup { get; } = new MarkerStyleGroup
        {
            GroupLabel = "Mismatched Elevation",
            ColorName = "Orange",
            RadiusMm = 150
        };

        public IReadOnlyList<string> ColorPalette { get; } = NamedColorHelper.PaletteNames;

        // ── Metrics (bound by the Metrics Card) ─────────────────────────────────
        [ObservableProperty]
        private ComparisonMetrics metrics = new ComparisonMetrics();

        // ── Log ───────────────────────────────────────────────────────────────
        public ObservableCollection<LogEntry> LogEntries { get; } = new ObservableCollection<LogEntry>();

        [ObservableProperty]
        private string statusMessage = "Ready";

        [ObservableProperty]
        private bool isBusy;

        private ComparisonResult _lastResult;

        public RoofComparisonViewModel(UIDocument uidoc, UIApplication app, ElementId roofAId, ElementId roofBId)
        {
            UIDoc = uidoc;
            App = app;
            RoofAId = roofAId;
            RoofBId = roofBId;

            RoofComparisonEventManager.Init();

            Recalculate();
        }

        // ── Recalculate ───────────────────────────────────────────────────────
        [RelayCommand]
        private void Recalculate()
        {
            IsBusy = true;
            StatusMessage = "Comparing...";
            AddLog(new LogEntry(LogLevel.Info,
                $"Comparing shape-editing points (position tolerance {PositionToleranceMm} mm, elevation tolerance {ElevationToleranceMm} mm)..."));

            RoofComparisonHandler.Payload = new ComparisonPayload
            {
                Mode = ComparisonMode.Recalculate,
                RoofAId = RoofAId,
                RoofBId = RoofBId,
                PositionToleranceMm = PositionToleranceMm,
                ElevationToleranceMm = ElevationToleranceMm,
                MissingMarkerGroup = MissingMarkerGroup,
                MismatchMarkerGroup = MismatchMarkerGroup,
                Log = AddLog,
                OnRecalculated = OnRecalculated
            };

            RoofComparisonEventManager.Event.Raise();
        }

        private void OnRecalculated(ComparisonResult result)
        {
            RunOnUiThread(() =>
            {
                IsBusy = false;

                if (!result.Success)
                {
                    StatusMessage = "Failed";
                    AddLog(new LogEntry(LogLevel.Error, $"Comparison failed: {result.ErrorMessage}"));
                    return;
                }

                _lastResult = result;
                Metrics = result.Metrics;
                StatusMessage = "Done";

                AddLog(new LogEntry(LogLevel.Success,
                    $"Compared {result.Metrics.TotalPointsA} vs {result.Metrics.TotalPointsB} points — " +
                    $"{result.Metrics.MatchedCount} matched, {result.Metrics.DifferencesCount} difference(s)."));

                PlaceMarkersCommand.NotifyCanExecuteChanged();
            });
        }

        // ── PlaceMarkers ──────────────────────────────────────────────────────
        [RelayCommand(CanExecute = nameof(CanPlaceMarkers))]
        private void PlaceMarkers()
        {
            IsBusy = true;
            StatusMessage = "Placing markers...";
            AddLog(new LogEntry(LogLevel.Info, "Placing comparison markers in the active view..."));

            RoofComparisonHandler.Payload = new ComparisonPayload
            {
                Mode = ComparisonMode.PlaceMarkers,
                RoofAId = RoofAId,
                RoofBId = RoofBId,
                PositionToleranceMm = PositionToleranceMm,
                ElevationToleranceMm = ElevationToleranceMm,
                MissingMarkerGroup = MissingMarkerGroup,
                MismatchMarkerGroup = MismatchMarkerGroup,
                Log = AddLog,
                OnRecalculated = OnRecalculated,
                OnMarkersPlaced = OnMarkersPlaced
            };

            RoofComparisonEventManager.Event.Raise();
        }

        private bool CanPlaceMarkers() => _lastResult != null && _lastResult.Success && !IsBusy;

        private void OnMarkersPlaced(int missingPlaced, int mismatchPlaced)
        {
            RunOnUiThread(() =>
            {
                AddLog(new LogEntry(LogLevel.Success,
                    $"Placed {missingPlaced} missing-point circle(s) and {mismatchPlaced} mismatched-elevation circle(s)."));
            });
        }

        // ── ClearLog ──────────────────────────────────────────────────────────
        [RelayCommand]
        private void ClearLog()
        {
            LogEntries.Clear();
            AddLog(new LogEntry(LogLevel.Info, "Log cleared."));
        }

        private void AddLog(LogEntry entry) => RunOnUiThread(() => LogEntries.Add(entry));

        private static void RunOnUiThread(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                action();
            else
                dispatcher.BeginInvoke(action);
        }
    }
}
