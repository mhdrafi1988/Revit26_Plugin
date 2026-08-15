using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Revit26_Plugin.VertexReducer.V005.Commands;
using Revit26_Plugin.VertexReducer.V005.Models;
using Revit26_Plugin.VertexReducer.V005.Services;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.VertexReducer.V005.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private RoofEdgeVertexReducerEventHandler _handler;
        private Autodesk.Revit.UI.ExternalEvent _externalEvent;

        [ObservableProperty] private string roofLabel = "Roof: (none selected)";
        [ObservableProperty] private double toleranceMm = 5.0;

        /// <summary>
        /// Outer-loop only. Number of positions (loop-traversal order) away from a
        /// qualifying max-Z point to also keep, on each side. 0 disables the rule.
        /// </summary>
        [ObservableProperty] private int neighborOffset = 1;

        [ObservableProperty] private bool canPreview;
        [ObservableProperty] private bool canApply;
        [ObservableProperty] private string summaryText = "No preview run yet";

        public ObservableCollection<VertexPreviewRow> PreviewRows { get; } = new();
        public ObservableCollection<LogEntry> Logs { get; } = new();

        public void Initialize(RoofEdgeVertexReducerEventHandler handler, Autodesk.Revit.UI.ExternalEvent externalEvent)
        {
            _handler = handler;
            _externalEvent = externalEvent;
        }

        [RelayCommand]
        private void SelectRoof()
        {
            _handler.PendingRequest = ReducerRequest.SelectRoof;
            _externalEvent.Raise();
        }

        [RelayCommand]
        private void Preview()
        {
            CanPreview = false;
            _handler.PendingRequest = ReducerRequest.Preview;
            _externalEvent.Raise();
        }

        [RelayCommand]
        private void Apply()
        {
            CanApply = false;
            _handler.PendingRequest = ReducerRequest.Apply;
            _externalEvent.Raise();
        }

        [RelayCommand]
        private void CopyAllLogs()
        {
            if (Logs.Count == 0) return;
            Clipboard.SetText(string.Join(Environment.NewLine, Logs.Select(l => l.ToString())));
        }

        [RelayCommand]
        private void ClearLogs() => Logs.Clear();

        public void CopySelectedLogs(IList selected)
        {
            if (selected == null || selected.Count == 0) return;
            var lines = selected.Cast<LogEntry>().Select(l => l.ToString());
            Clipboard.SetText(string.Join(Environment.NewLine, lines));
        }

        /// <summary>Called from the event handler after a roof pick completes.</summary>
        public void SetSelectedRoof(string label)
        {
            RoofLabel = label;
            PreviewRows.Clear();
            CanPreview = true;
            CanApply = false;
            SummaryText = "No preview run yet";
        }

        /// <summary>Called from the event handler after ClassifyAndReduce completes.</summary>
        public void PopulatePreview(List<VertexDecision> decisions)
        {
            PreviewRows.Clear();
            int idx = 0;
            foreach (var d in decisions.OrderBy(d => d.SegmentLabel))
            {
                idx++;
                PreviewRows.Add(new VertexPreviewRow
                {
                    Segment = d.SegmentLabel,
                    PointId = $"#{idx}",
                    Z = $"{EdgeVertexReducerService.FeetToMm(d.ZFeet):0.0} mm",
                    Action = ActionText(d),
                    ActionColorKey = ActionColorKey(d)
                });
            }
            CanApply = true;
        }

        private static string ActionText(VertexDecision d) => d.Action switch
        {
            VertexAction.KeepStart => "Keep (start)",
            VertexAction.KeepEnd => "Keep (end)",
            VertexAction.KeepMaxZ => "Keep (max Z)",
            VertexAction.KeepNeighbor => "Keep (neighbor)",
            VertexAction.KeepUnmatched => "Keep (unmatched)",
            VertexAction.Remove => "Remove",
            _ => "\u2014"
        };

        private static string ActionColorKey(VertexDecision d) =>
            d.Action == VertexAction.Remove ? "Danger" :
            d.Action == VertexAction.KeepMaxZ ? "Success" :
            d.Action == VertexAction.KeepNeighbor ? "Success" : "Secondary";

        public void AddLog(LogLevel level, string message) => Logs.Add(new LogEntry(level, message));

        public void SetSummary(string text) => SummaryText = text;
    }
}
