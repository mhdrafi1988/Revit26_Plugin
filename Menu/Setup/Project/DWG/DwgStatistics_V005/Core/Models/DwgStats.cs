using System;
using System.Collections.Generic;

namespace Revit26_Plugin_Revised.DwgStatistics.V005.Core.Models
{
    /// <summary>How serious an issue badge is; drives its colour.</summary>
    public enum IssueSeverity
    {
        /// <summary>Worth knowing (model placement, unpinned, absolute path…).</summary>
        Info,

        /// <summary>Should be cleaned up (imported, unused, duplicate, shows in 3D).</summary>
        Warning,

        /// <summary>Broken (link not found / not loaded).</summary>
        Danger
    }

    /// <summary>One issue badge on a DWG file row.</summary>
    public sealed class DwgIssue
    {
        /// <summary>Creates an issue.</summary>
        public DwgIssue(string text, IssueSeverity severity, string detail, string fix)
        {
            Text = text;
            Severity = severity;
            Detail = detail;
            Fix = fix;
        }

        /// <summary>Short badge text ("Not found").</summary>
        public string Text { get; }

        /// <summary>Badge colour.</summary>
        public IssueSeverity Severity { get; }

        /// <summary>What the issue means.</summary>
        public string Detail { get; }

        /// <summary>V004: how to fix it (tooltip second line, and the Issues &amp; Fixes export sheet).</summary>
        public string Fix { get; }

        /// <summary>Tooltip: meaning + fix.</summary>
        public string ToolTip => string.IsNullOrEmpty(Fix) ? Detail : $"{Detail}\nFix: {Fix}";

        /// <summary>Severity as text, for XAML triggers.</summary>
        public string SeverityName => Severity.ToString();
    }

    /// <summary>The quick-filter chips above the file grid.</summary>
    public enum DwgQuickFilter
    {
        /// <summary>Every file.</summary>
        All,

        /// <summary>Linked files only.</summary>
        Links,

        /// <summary>Imported files only.</summary>
        Imports,

        /// <summary>Links that are not loaded (not found, unloaded…).</summary>
        NotLoaded,

        /// <summary>Types with no instance.</summary>
        Unused,

        /// <summary>Files with at least one warning or danger issue.</summary>
        Issues,

        /// <summary>V004: files that are new or changed since the last scan.</summary>
        Changed
    }

    /// <summary>One CAD instance with its resolved view names, for the Instances tab and the export.</summary>
    public sealed class DwgInstanceStats
    {
        /// <summary>Element ID.</summary>
        public long Id { get; init; }

        /// <summary>"View-specific" or "Model".</summary>
        public string Placement { get; init; } = string.Empty;

        /// <summary>True when placed "Current view only".</summary>
        public bool IsViewSpecific { get; init; }

        /// <summary>Owner view ID, or 0.</summary>
        public long OwnerViewId { get; init; }

        /// <summary>Owner view name, or "(all views)".</summary>
        public string OwnerViewName { get; init; } = string.Empty;

        /// <summary>Number of views that draw this instance.</summary>
        public int VisibleViewCount { get; init; }

        /// <summary>V004: number of views where this instance is hidden by element.</summary>
        public int HiddenViewCount { get; init; }

        /// <summary>A view that draws this instance (owner view first), used to open and zoom; 0 if none.</summary>
        public long BestViewId { get; init; }

        /// <summary>True when pinned.</summary>
        public bool Pinned { get; init; }

        /// <summary>"Yes" / "No".</summary>
        public string PinnedText => Pinned ? "Yes" : "No";

        /// <summary>Workset name.</summary>
        public string Workset { get; init; } = string.Empty;

        /// <summary>Level name.</summary>
        public string Level { get; init; } = string.Empty;

        /// <summary>Creator (workshared models).</summary>
        public string CreatedBy { get; init; } = string.Empty;

        /// <summary>V005: farthest point of the instance's geometry from the internal origin, in km.</summary>
        public double DistanceKm { get; init; }

        /// <summary>V005: "0.12" style distance text.</summary>
        public string DistanceText => DistanceKm.ToString(DistanceKm < 10 ? "0.00" : "0.0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>V005: true when the workset passes the CAD workset rule (always true when not workshared).</summary>
        public bool WorksetOk { get; init; } = true;

        /// <summary>V005: "Yes" / "No" / "—".</summary>
        public string WorksetOkText { get; init; } = "—";

        /// <summary>V005: true when Move to Workset can change it (model-placed, not read-only).</summary>
        public bool CanChangeWorkset { get; init; }
    }

    /// <summary>V005: one DWG layer of a file, for the Layers tab and the export.</summary>
    public sealed class DwgLayerStats
    {
        /// <summary>Sub-category element ID.</summary>
        public long LayerId { get; init; }

        /// <summary>Layer name.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Views (where the file shows) in which this layer is hidden.</summary>
        public List<long> HiddenViewIds { get; init; } = new List<long>();

        /// <summary>Views in which this layer has graphic overrides.</summary>
        public List<long> OverriddenViewIds { get; init; } = new List<long>();

        /// <summary>Hidden-view names, comma separated (first 6).</summary>
        public string HiddenViewNames { get; init; } = string.Empty;

        /// <summary>Number of views hiding the layer.</summary>
        public int HiddenInViews => HiddenViewIds.Count;

        /// <summary>Number of views overriding the layer.</summary>
        public int OverriddenInViews => OverriddenViewIds.Count;
    }

    /// <summary>One view that owns, draws or hides a DWG file, for the Views tab and the export.</summary>
    public sealed class DwgViewUsage
    {
        /// <summary>Element ID of the view.</summary>
        public long ViewId { get; init; }

        /// <summary>View name.</summary>
        public string ViewName { get; init; } = string.Empty;

        /// <summary>View type label.</summary>
        public string ViewType { get; init; } = string.Empty;

        /// <summary>Sheet numbers, comma separated, or empty.</summary>
        public string Sheets { get; init; } = string.Empty;

        /// <summary>V004: sheet element IDs (first one is used by "Open sheet").</summary>
        public List<long> SheetIds { get; init; } = new List<long>();

        /// <summary>V004: instances of the file owned by this view ("Current view only").</summary>
        public List<long> PlacedInstanceIds { get; init; } = new List<long>();

        /// <summary>V004: instances of the file drawn in this view.</summary>
        public List<long> VisibleInstanceIds { get; init; } = new List<long>();

        /// <summary>V004: model instances of the file hidden by element in this view.</summary>
        public List<long> HiddenInstanceIds { get; init; } = new List<long>();

        /// <summary>True when the view owns at least one view-specific instance of the file.</summary>
        public bool IsPlaced => PlacedInstanceIds.Count > 0;

        /// <summary>Instances of the file owned by the view.</summary>
        public int PlacedInstances => PlacedInstanceIds.Count;

        /// <summary>Instances of the file drawn in the view.</summary>
        public int VisibleInstances => VisibleInstanceIds.Count;

        /// <summary>V004: instances hidden in the view.</summary>
        public int HiddenInstances => HiddenInstanceIds.Count;

        /// <summary>True for 3D views.</summary>
        public bool Is3D => ViewType == "3D Views";

        /// <summary>V005: layers of the file hidden in this view.</summary>
        public int HiddenLayers { get; init; }

        /// <summary>V005: layers of the file overridden in this view.</summary>
        public int OverriddenLayers { get; init; }

        /// <summary>"Placed here", "Visible (model)" or "Hidden here".</summary>
        public string Relation
        {
            get
            {
                if (IsPlaced) return "Placed here";
                if (VisibleInstances > 0) return HiddenInstances > 0 ? "Visible (some hidden)" : "Visible (model)";
                return "Hidden here";
            }
        }
    }

    /// <summary>Statistics of one DWG file (one CAD type) — one row of the main grid.</summary>
    public sealed class DwgFileStats
    {
        /// <summary>Element ID of the CAD type.</summary>
        public long TypeId { get; init; }

        /// <summary>File name.</summary>
        public string FileName { get; init; } = string.Empty;

        /// <summary>True for a link, false for an import.</summary>
        public bool IsLinked { get; init; }

        /// <summary>"Link" / "Import".</summary>
        public string Kind => IsLinked ? "Link" : "Import";

        /// <summary>Revit link status, or "Imported".</summary>
        public string Status { get; init; } = string.Empty;

        /// <summary>True when loaded (always true for imports).</summary>
        public bool IsLoaded { get; init; }

        /// <summary>Full path, or empty.</summary>
        public string Path { get; init; } = string.Empty;

        /// <summary>Absolute / Relative…</summary>
        public string PathType { get; init; } = string.Empty;

        /// <summary>File size on disk.</summary>
        public long? FileSizeBytes { get; init; }

        /// <summary>File date on disk.</summary>
        public DateTime? FileModified { get; init; }

        /// <summary>Size as "1.2 MB", or "—".</summary>
        public string FileSizeText => FormatSize(FileSizeBytes);

        /// <summary>Total instances.</summary>
        public int InstanceCount { get; init; }

        /// <summary>Instances placed "Current view only".</summary>
        public int ViewSpecificCount { get; init; }

        /// <summary>Instances placed in the model (all views).</summary>
        public int ModelCount { get; init; }

        /// <summary>Pinned instances.</summary>
        public int PinnedCount { get; init; }

        /// <summary>Unpinned instances.</summary>
        public int UnpinnedCount => InstanceCount - PinnedCount;

        /// <summary>"3 / 5".</summary>
        public string PinnedText => InstanceCount == 0 ? "—" : $"{PinnedCount} / {InstanceCount}";

        /// <summary>Distinct views that own a view-specific instance.</summary>
        public int PlacedViewCount { get; init; }

        /// <summary>Distinct views that own or draw any instance (deep scan).</summary>
        public int VisibleViewCount { get; init; }

        /// <summary>V004: distinct views where an instance is hidden by element.</summary>
        public int HiddenViewCount { get; init; }

        /// <summary>V004: distinct 3D views that draw a model instance.</summary>
        public int ThreeDViewCount { get; init; }

        /// <summary>Distinct sheets holding any of those views.</summary>
        public int SheetCount { get; init; }

        /// <summary>CAD layers (max over its instances), or 0 when unused.</summary>
        public int LayerCount { get; init; }

        /// <summary>Issue badges.</summary>
        public List<DwgIssue> Issues { get; init; } = new List<DwgIssue>();

        /// <summary>Issues joined for the export ("Imported; Unpinned").</summary>
        public string IssueText => string.Join("; ", Issues.ConvertAll(i => i.Text));

        /// <summary>True when any issue is a warning or worse.</summary>
        public bool HasProblem => Issues.Exists(i => i.Severity != IssueSeverity.Info);

        /// <summary>Instances of the file.</summary>
        public List<DwgInstanceStats> Instances { get; init; } = new List<DwgInstanceStats>();

        /// <summary>Views that own, draw or hide the file.</summary>
        public List<DwgViewUsage> Views { get; init; } = new List<DwgViewUsage>();

        /// <summary>V005: import category of the file (0 when unused).</summary>
        public long CategoryId { get; init; }

        /// <summary>V005: farthest point of any instance from the internal origin, in km.</summary>
        public double MaxDistanceKm { get; init; }

        /// <summary>V005: "0.12" style text, or "—" when unused.</summary>
        public string MaxDistanceText => InstanceCount == 0 ? "—"
            : MaxDistanceKm.ToString(MaxDistanceKm < 10 ? "0.00" : "0.0", System.Globalization.CultureInfo.InvariantCulture);

        /// <summary>V005: instances on a workset that fails the CAD workset rule.</summary>
        public int WrongWorksetCount { get; init; }

        /// <summary>V005: of those, how many Move to Workset can change.</summary>
        public int MovableWrongWorksetCount { get; init; }

        /// <summary>V005: layers of the file with their per-view state.</summary>
        public List<DwgLayerStats> Layers { get; init; } = new List<DwgLayerStats>();

        /// <summary>V005: true when the file on disk is newer than at the last scan (set by the history compare).</summary>
        public bool IsUpdatedOnDisk { get; set; }

        /// <summary>V004: "New" / "Changed" / empty, compared with the last scan.</summary>
        public string ChangeTag { get; set; } = string.Empty;

        /// <summary>V004: what changed ("instances 2 → 3; views 4 → 6").</summary>
        public string ChangeDetail { get; set; } = string.Empty;

        /// <summary>"1.2 MB" style size text.</summary>
        public static string FormatSize(long? bytes)
        {
            if (bytes == null) return "—";
            double b = bytes.Value;
            if (b < 1024) return $"{b:0} B";
            if (b < 1024 * 1024) return $"{b / 1024:0.0} KB";
            return $"{b / (1024 * 1024):0.0} MB";
        }
    }

    /// <summary>Project-wide totals for the header tiles.</summary>
    public sealed class DwgProjectTotals
    {
        /// <summary>CAD files (types).</summary>
        public int Files { get; init; }

        /// <summary>Linked files.</summary>
        public int Links { get; init; }

        /// <summary>Imported files.</summary>
        public int Imports { get; init; }

        /// <summary>CAD instances.</summary>
        public int Instances { get; init; }

        /// <summary>Distinct views that own or draw CAD.</summary>
        public int Views { get; init; }

        /// <summary>Distinct sheets showing CAD.</summary>
        public int Sheets { get; init; }

        /// <summary>Links not loaded.</summary>
        public int NotLoaded { get; init; }

        /// <summary>Files with a warning or danger issue.</summary>
        public int WithIssues { get; init; }

        /// <summary>Types with no instance.</summary>
        public int Unused { get; init; }
    }

    /// <summary>Counts from one cleanup or view action.</summary>
    public sealed class DwgActionResult
    {
        /// <summary>Action name for the summary ("Pin").</summary>
        public string Action { get; init; } = string.Empty;

        /// <summary>Items changed.</summary>
        public int Done { get; set; }

        /// <summary>Items left alone (already done, owned by another user, not applicable).</summary>
        public int Skipped { get; set; }

        /// <summary>Items Revit refused.</summary>
        public int Failed { get; set; }

        /// <summary>V004: CAD types purged after a delete.</summary>
        public int TypesPurged { get; set; }

        /// <summary>True when Revit rolled the transaction back.</summary>
        public bool RolledBack { get; set; }

        /// <summary>"Pin: 4 done | 1 skipped | 0 failed".</summary>
        public string Summary => RolledBack
            ? $"{Action}: rolled back — nothing was changed."
            : $"{Action}: {Done} done | {Skipped} skipped | {Failed} failed" +
              (TypesPurged > 0 ? $" | {TypesPurged} type(s) purged" : string.Empty);
    }
}
