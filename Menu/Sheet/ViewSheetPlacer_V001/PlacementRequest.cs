using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.Tools.ViewSheetPlacer
{
    /// <summary>Grouping strategy for selected views before packing.</summary>
    public enum GroupMode { Discipline, ViewType }

    /// <summary>
    /// Everything the handler needs for one run. Built on the UI thread by the
    /// ViewModel, consumed on the Revit API thread by the handler.
    /// </summary>
    public sealed class PlacementRequest
    {
        public IReadOnlyList<ViewInfo> SelectedViews { get; init; } = Array.Empty<ViewInfo>();
        public ElementId TitleblockTypeId { get; init; } = ElementId.InvalidElementId;
        public string SheetNamePrefix { get; init; } = string.Empty;
        public GroupMode Grouping { get; init; } = GroupMode.Discipline;
        public bool SkipAlreadyPlaced { get; init; } = true;
        public bool ShowViewportTitles { get; init; } = true;

        public double SheetMarginMm { get; init; } = 15.0;
        public double ViewportGapMm { get; init; } = 10.0;

        /// <summary>Right-side strip reserved for the titleblock's title band (mm).</summary>
        public double TitleStripMm { get; init; } = 0.0;

        /// <summary>True = preview counts only, no model changes.</summary>
        public bool DryRun { get; init; }

        /// <summary>Called (marshalled to the UI dispatcher) for every log line.</summary>
        public Action<LogEntry>? Log { get; init; }

        /// <summary>Called once when the run finishes: (placed, skipped, failed).</summary>
        public Action<int, int, int>? OnComplete { get; init; }
    }
}
