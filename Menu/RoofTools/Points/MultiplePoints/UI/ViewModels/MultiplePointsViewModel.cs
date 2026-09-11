// =======================================================
// File: MultiplePointsViewModel.cs
// Location: UI/ViewModels/
// Constructor takes (ElementId roofId, initial edges) — a modeless
// window cannot hold a live Document/RoofBase and call Revit API
// methods on it directly from a button click. Apply() raises the
// ExternalEvent instead of calling EdgePointService directly; the roof
// is re-resolved from RoofId inside MultiplePointsEngine, which only
// ever runs inside a valid API context (Command or ExternalEvent
// handler). The first edge-extraction pass is done by the Command
// before the window opens and passed in via initialEdges.
// =======================================================

using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.MultiplePoints.V001.Core.Models;
using Revit26_Plugin.MultiplePoints.V001.Core.Services;
using Revit26_Plugin.MultiplePoints.V001.Infrastructure.ExternalEvents;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;

namespace Revit26_Plugin.MultiplePoints.V001.UI.ViewModels
{
    public partial class MultiplePointsViewModel : ObservableObject
    {
        private readonly ElementId _roofId;
        private readonly EdgePointService _service = new EdgePointService();

        /// <summary>Every perimeter edge EdgePointService found, unfiltered — Edges is this filtered by the max-slope cap.</summary>
        private System.Collections.Generic.List<EdgePointModel> _allEdges = new System.Collections.Generic.List<EdgePointModel>();

        public MultiplePointsSettings Settings { get; } = new MultiplePointsSettings();

        public ObservableCollection<EdgePointModel> Edges { get; } = new ObservableCollection<EdgePointModel>();

        /// <summary>Shared log rows (timestamp + level + message), colored by LogLevelToColorConverter.</summary>
        public ObservableCollection<LogEntry> LogEntries { get; } = new ObservableCollection<LogEntry>();

        [ObservableProperty]
        private int totalPreviewPoints;

        public MultiplePointsViewModel(ElementId roofId, System.Collections.Generic.List<EdgePointModel> initialEdges)
        {
            _roofId = roofId;

            Settings.PropertyChanged += OnSettingsChanged;

            PopulateEdges(initialEdges);
            RefreshPreview();

            MultiplePointsEventManager.Init();
        }

        private void PopulateEdges(System.Collections.Generic.List<EdgePointModel> found)
        {
            _allEdges = found ?? new System.Collections.Generic.List<EdgePointModel>();

            Log(LogLevel.Info, $"Roof loaded — {_allEdges.Count} edge(s) found on the top face.");
            if (_allEdges.Count == 0) Log(LogLevel.Warning, "No edges found on this roof's top face.");

            ApplySlopeFilter();
        }

        private void OnSettingsChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(MultiplePointsSettings.LimitMaxSlope) ||
                e.PropertyName == nameof(MultiplePointsSettings.MaxSlopePercent))
            {
                ApplySlopeFilter();
            }
            RefreshPreview();
        }

        /// <summary>Rebuilds the displayed Edges from _allEdges per the current max-slope cap — a display filter only, no re-extraction from Revit.</summary>
        private void ApplySlopeFilter()
        {
            Edges.Clear();

            var visible = Settings.LimitMaxSlope
                ? _allEdges.Where(e => e.FacetSlopePercent <= Settings.MaxSlopePercent)
                : _allEdges;

            foreach (var edge in visible) Edges.Add(edge);

            if (Settings.LimitMaxSlope)
            {
                int hidden = _allEdges.Count - Edges.Count;
                string suffix = hidden > 0 ? $" ({hidden} excluded — facet too steep)." : ".";
                Log(LogLevel.Info, $"Slope filter ≤ {Settings.MaxSlopePercent:F1}%: {Edges.Count} of {_allEdges.Count} edge(s) shown{suffix}");
            }
        }

        private void RefreshPreview()
        {
            int total = 0;
            foreach (var edge in Edges)
            {
                int count = _service.GetTargetFractions(edge.LengthM, Settings).Count;
                edge.PreviewPointCount = count;
                total += count;
            }
            TotalPreviewPoints = total;
        }

        [RelayCommand]
        private void SelectAll() => SetAllSelected(true);

        [RelayCommand]
        private void SelectNone() => SetAllSelected(false);

        private void SetAllSelected(bool selected)
        {
            foreach (var edge in Edges) edge.IsSelected = selected;
        }

        [RelayCommand]
        private void Apply()
        {
            var selected = Edges.Where(e => e.IsSelected).ToList();
            if (!selected.Any()) { Log(LogLevel.Warning, "Apply skipped — no edges selected."); return; }

            if (!Settings.AddMidpoint && !Settings.AddQuarterPoints && !Settings.AddExtraOnLongEdges)
            {
                Log(LogLevel.Warning, "Apply skipped — no point type is checked.");
                return;
            }

            int totalPts = selected.Sum(e => e.PreviewPointCount);
            Log(LogLevel.Info, $"Applying to {selected.Count} edge(s): {totalPts} point(s) expected...");

            MultiplePointsHandler.Payload = new MultiplePointsPayload
            {
                RoofId              = _roofId,
                SelectedEdges       = selected,
                AddMidpoint         = Settings.AddMidpoint,
                AddQuarterPoints    = Settings.AddQuarterPoints,
                AddExtraOnLongEdges = Settings.AddExtraOnLongEdges,
                ThresholdMeters     = Settings.ThresholdMeters,
                Log = AddLogEntry,
                OnCompleted = result =>
                {
                    System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (!result.Success)
                        {
                            Log(LogLevel.Error, $"Apply failed: {result.ErrorMessage}");
                            return;
                        }

                        if (result.LogEntries != null)
                            foreach (var entry in result.LogEntries)
                                AddLogEntry(entry);
                    }));
                }
            };

            MultiplePointsEventManager.Event.Raise();
        }

        private void Log(LogLevel level, string message) => AddLogEntry(new LogEntry(level, message));

        private void AddLogEntry(LogEntry entry)
        {
            var dispatcher = System.Windows.Application.Current.Dispatcher;
            if (dispatcher.CheckAccess())
                LogEntries.Add(entry);
            else
                dispatcher.BeginInvoke(new Action(() => LogEntries.Add(entry)));
        }
    }
}
