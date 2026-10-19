using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Revit26_Plugin.DwgStatistics.V005.Core.Models;

namespace Revit26_Plugin.DwgStatistics.V005.UI.ViewModels
{
    /// <summary>
    /// One row of the file grid: the statistics of one DWG file, flattened for binding and sorting,
    /// plus the tick used by the cleanup actions.
    /// </summary>
    public partial class DwgFileRowViewModel : ObservableObject
    {
        /// <summary>Creates a row for <paramref name="stats"/>.</summary>
        public DwgFileRowViewModel(DwgFileStats stats, ISet<long> tickedViewIds = null, ISet<long> tickedLayerIds = null)
        {
            Stats = stats ?? throw new ArgumentNullException(nameof(stats));

            // V002/V004: Views tab rows with their own tick (restored from the previous scan when given).
            ViewRows = new ObservableCollection<DwgViewRowViewModel>(stats.Views.Select(v =>
                new DwgViewRowViewModel(v) { IsTicked = tickedViewIds != null && tickedViewIds.Contains(v.ViewId) }));
            foreach (var row in ViewRows) row.PropertyChanged += OnViewRowChanged;
            TickedViewCount = ViewRows.Count(r => r.IsTicked);

            // V005: Layers tab rows with their own tick.
            LayerRows = new ObservableCollection<DwgLayerRowViewModel>(stats.Layers.Select(l =>
                new DwgLayerRowViewModel(l) { IsTicked = tickedLayerIds != null && tickedLayerIds.Contains(l.LayerId) }));
            foreach (var row in LayerRows) row.PropertyChanged += OnLayerRowChanged;
            TickedLayerCount = LayerRows.Count(r => r.IsTicked);
        }

        /// <summary>V005: rows of the Layers tab.</summary>
        public ObservableCollection<DwgLayerRowViewModel> LayerRows { get; }

        /// <summary>V005: layers ticked in the Layers tab.</summary>
        [ObservableProperty] private int tickedLayerCount;

        /// <summary>V005: IDs of the ticked layers.</summary>
        public List<long> TickedLayerIds => LayerRows.Where(r => r.IsTicked).Select(r => r.LayerId).ToList();

        /// <summary>V005: "Layers (46)".</summary>
        public string LayersTabHeader => $"Layers ({LayerRows.Count})";

        /// <summary>V005: ticks or unticks every layer.</summary>
        public void SetAllLayerTicks(bool on)
        {
            foreach (var row in LayerRows) row.IsTicked = on;
        }

        private void OnLayerRowChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DwgLayerRowViewModel.IsTicked))
                TickedLayerCount = LayerRows.Count(r => r.IsTicked);
        }

        /// <summary>V005: farthest geometry from the internal origin, km text.</summary>
        public string MaxDistanceText => Stats.MaxDistanceText;

        /// <summary>V005: for sorting the distance column.</summary>
        public double MaxDistanceKm => Stats.MaxDistanceKm;

        /// <summary>V005: instances failing the CAD workset rule.</summary>
        public int WrongWorksetCount => Stats.WrongWorksetCount;

        /// <summary>V005: model-placed instances (what Move to Workset moves).</summary>
        public int MovableCount => Stats.Instances.Count(i => i.CanChangeWorkset);

        /// <summary>V005: the DWG on disk is newer than at the last scan.</summary>
        public bool IsUpdatedOnDisk => Stats.IsUpdatedOnDisk;

        /// <summary>The statistics behind the row.</summary>
        public DwgFileStats Stats { get; }

        /// <summary>Ticked for Pin All / Reload / Purge.</summary>
        [ObservableProperty] private bool isTicked;

        /// <summary>V002: rows of the Views tab, each with a tick for Open Ticked Views.</summary>
        public ObservableCollection<DwgViewRowViewModel> ViewRows { get; }

        /// <summary>V002: views ticked in the Views tab (V004: the Open/Hide/Unhide/Delete labels live on the main view model).</summary>
        [ObservableProperty] private int tickedViewCount;

        /// <summary>V002: IDs of the ticked views, in list order.</summary>
        public List<long> TickedViewIds => ViewRows.Where(r => r.IsTicked).Select(r => r.ViewId).ToList();

        /// <summary>V004: the ticked view rows, in list order.</summary>
        public List<DwgViewRowViewModel> TickedViewRows => ViewRows.Where(r => r.IsTicked).ToList();

        /// <summary>V004: "New" / "Changed" / empty since the last scan.</summary>
        public string ChangeTag => Stats.ChangeTag;

        /// <summary>V004: what changed since the last scan.</summary>
        public string ChangeDetail => Stats.ChangeDetail;

        /// <summary>V004: true when the file is new or changed since the last scan.</summary>
        public bool HasChange => !string.IsNullOrEmpty(Stats.ChangeTag);

        /// <summary>V004: 3D views drawing a model instance.</summary>
        public int ThreeDViewCount => Stats.ThreeDViewCount;

        /// <summary>V004: views where an instance is hidden by element.</summary>
        public int HiddenViewCount => Stats.HiddenViewCount;

        /// <summary>V002: ticks or unticks every view in the Views tab.</summary>
        public void SetAllViewTicks(bool on)
        {
            foreach (var row in ViewRows) row.IsTicked = on;
        }

        private void OnViewRowChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(DwgViewRowViewModel.IsTicked))
                TickedViewCount = ViewRows.Count(r => r.IsTicked);
        }

        /// <summary>CAD type element ID.</summary>
        public long TypeId => Stats.TypeId;

        /// <summary>File name.</summary>
        public string FileName => Stats.FileName;

        /// <summary>Tooltip: full path and file details when known.</summary>
        public string FileToolTip
        {
            get
            {
                if (string.IsNullOrEmpty(Stats.Path)) return Stats.IsLinked ? $"{FileName}\n(path unavailable)" : $"{FileName}\n(imported — stored in the model)";
                string details = Stats.FileSizeBytes.HasValue
                    ? $"\n{Stats.FileSizeText} · modified {Stats.FileModified:yyyy-MM-dd HH:mm}"
                    : "\n(file not found on this computer)";
                string pathType = string.IsNullOrEmpty(Stats.PathType) ? string.Empty : $"\nPath type: {Stats.PathType}";
                return Stats.Path + pathType + details;
            }
        }

        /// <summary>"Link" / "Import".</summary>
        public string Kind => Stats.Kind;

        /// <summary>True for a link.</summary>
        public bool IsLinked => Stats.IsLinked;

        /// <summary>Link status, or "Imported".</summary>
        public string Status => Stats.Status;

        /// <summary>True when loaded.</summary>
        public bool IsLoaded => Stats.IsLoaded;

        /// <summary>Total instances.</summary>
        public int InstanceCount => Stats.InstanceCount;

        /// <summary>View-specific instances.</summary>
        public int ViewSpecificCount => Stats.ViewSpecificCount;

        /// <summary>Model instances.</summary>
        public int ModelCount => Stats.ModelCount;

        /// <summary>Views that own an instance.</summary>
        public int PlacedViewCount => Stats.PlacedViewCount;

        /// <summary>Views that own or draw an instance.</summary>
        public int VisibleViewCount => Stats.VisibleViewCount;

        /// <summary>Sheets showing the file.</summary>
        public int SheetCount => Stats.SheetCount;

        /// <summary>Pinned instances.</summary>
        public int PinnedCount => Stats.PinnedCount;

        /// <summary>Unpinned instances.</summary>
        public int UnpinnedCount => Stats.UnpinnedCount;

        /// <summary>"3 / 5".</summary>
        public string PinnedText => Stats.PinnedText;

        /// <summary>CAD layers.</summary>
        public int LayerCount => Stats.LayerCount;

        /// <summary>Issue badges.</summary>
        public IReadOnlyList<DwgIssue> Issues => Stats.Issues;

        /// <summary>Number of issues, for sorting the Issues column.</summary>
        public int IssueCount => Stats.Issues.Count;

        /// <summary>True when any issue is a warning or worse.</summary>
        public bool HasProblem => Stats.HasProblem;

        /// <summary>Views tab rows.</summary>
        public IReadOnlyList<DwgViewUsage> Views => Stats.Views;

        /// <summary>Instances tab rows.</summary>
        public IReadOnlyList<DwgInstanceStats> Instances => Stats.Instances;

        /// <summary>"Views (12)".</summary>
        public string ViewsTabHeader => $"Views ({Stats.Views.Count})";

        /// <summary>"Instances (4)".</summary>
        public string InstancesTabHeader => $"Instances ({Stats.Instances.Count})";

        /// <summary>One-line summary above the detail tabs.</summary>
        public string DetailSummary =>
            $"{Stats.Kind} · {Stats.Status} · {Stats.InstanceCount} instance(s) " +
            $"({Stats.ViewSpecificCount} view-specific, {Stats.ModelCount} model) · " +
            $"placed in {Stats.PlacedViewCount} view(s) · shown in {Stats.VisibleViewCount} view(s) · " +
            $"on {Stats.SheetCount} sheet(s) · hidden in {Stats.HiddenViewCount} view(s) · {Stats.LayerCount} layer(s) · " +
            $"up to {Stats.MaxDistanceText} km from origin" +
            (Stats.WrongWorksetCount > 0 ? $" · {Stats.WrongWorksetCount} on a non-CAD workset" : string.Empty) +
            (HasChange ? $" · since last scan: {Stats.ChangeTag} ({Stats.ChangeDetail})" : string.Empty);
    }
}
