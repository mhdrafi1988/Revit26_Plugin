using System;
using System.Collections.Generic;

namespace Revit26_Plugin.DwgStatistics.V005.Core.Models
{
    /// <summary>
    /// V004: what one scan looked like, saved per project as JSON so the next session can show what
    /// changed ("Compare with last scan").
    /// </summary>
    public sealed class DwgSnapshot
    {
        /// <summary>When the scan ran.</summary>
        public DateTime ScannedAt { get; set; }

        /// <summary>Project title at that time.</summary>
        public string ProjectTitle { get; set; } = string.Empty;

        /// <summary>Tool label that wrote it ("DWG Statistics V005").</summary>
        public string Tool { get; set; } = string.Empty;

        /// <summary>V005: Windows user who ran the scan (shared history).</summary>
        public string ScannedBy { get; set; } = string.Empty;

        /// <summary>One entry per DWG file.</summary>
        public List<DwgSnapshotEntry> Files { get; set; } = new List<DwgSnapshotEntry>();
    }

    /// <summary>One DWG file in a snapshot.</summary>
    public sealed class DwgSnapshotEntry
    {
        /// <summary>Match key: kind + path (or name when there is no path), lower case.</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>CAD type element ID at that time.</summary>
        public long TypeId { get; set; }

        /// <summary>File name.</summary>
        public string FileName { get; set; } = string.Empty;

        /// <summary>"Link" / "Import".</summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>Link status.</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Instances.</summary>
        public int Instances { get; set; }

        /// <summary>Views that own or draw it.</summary>
        public int Views { get; set; }

        /// <summary>Sheets.</summary>
        public int Sheets { get; set; }

        /// <summary>Pinned instances.</summary>
        public int Pinned { get; set; }

        /// <summary>V005: last write time of the file on disk (UTC), when known.</summary>
        public DateTime? FileModifiedUtc { get; set; }

        /// <summary>V005: file size on disk, when known.</summary>
        public long? FileSizeBytes { get; set; }
    }

    /// <summary>V005: one line of the shared scan history (who scanned when, and what they found).</summary>
    public sealed class DwgHistoryEntry
    {
        /// <summary>When the scan ran.</summary>
        public DateTime ScannedAt { get; set; }

        /// <summary>Windows user.</summary>
        public string ScannedBy { get; set; } = string.Empty;

        /// <summary>Tool label.</summary>
        public string Tool { get; set; } = string.Empty;

        /// <summary>DWG files.</summary>
        public int Files { get; set; }

        /// <summary>Instances.</summary>
        public int Instances { get; set; }

        /// <summary>Files with warning/danger issues.</summary>
        public int WithIssues { get; set; }

        /// <summary>Changes found against the previous scan.</summary>
        public int Changes { get; set; }
    }

    /// <summary>
    /// V005: the history file kept next to the project (shared) or in %AppData% (fallback): the latest
    /// snapshot to compare against, plus a rolling log of recent scans.
    /// </summary>
    public sealed class DwgHistoryFile
    {
        /// <summary>File format version.</summary>
        public int Format { get; set; } = 5;

        /// <summary>Project title (for people browsing the folder).</summary>
        public string ProjectTitle { get; set; } = string.Empty;

        /// <summary>The latest scan.</summary>
        public DwgSnapshot Latest { get; set; }

        /// <summary>Recent scans, newest first (capped).</summary>
        public List<DwgHistoryEntry> History { get; set; } = new List<DwgHistoryEntry>();
    }

    /// <summary>V005: the configurable rules behind the new health checks.</summary>
    public sealed class DwgRules
    {
        /// <summary>Flag DWGs whose geometry reaches farther than this from the internal origin (km).</summary>
        public double FarFromOriginKm { get; set; } = 10;

        /// <summary>Flag DWGs with more layers than this.</summary>
        public int MaxLayers { get; set; } = 100;

        /// <summary>
        /// Workset names that count as "CAD worksets": semicolon-separated, case-insensitive "contains"
        /// (default "CAD;Link"). Empty = rule off.
        /// </summary>
        public string CadWorksetPattern { get; set; } = "CAD;Link";

        /// <summary>True when <paramref name="worksetName"/> passes the rule (rule off → always true).</summary>
        public bool IsCadWorkset(string worksetName)
        {
            var parts = (CadWorksetPattern ?? string.Empty)
                .Split(new[] { ';', ',' }, StringSplitOptions.RemoveEmptyEntries);
            bool any = false;
            foreach (var raw in parts)
            {
                var part = raw.Trim();
                if (part.Length == 0) continue;
                any = true;
                if ((worksetName ?? string.Empty).IndexOf(part, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return !any;
        }

        /// <summary>A copy with values clamped to sensible ranges.</summary>
        public DwgRules Normalized() => new DwgRules
        {
            FarFromOriginKm = double.IsNaN(FarFromOriginKm) || FarFromOriginKm <= 0 ? 10 : Math.Min(FarFromOriginKm, 100000),
            MaxLayers = MaxLayers <= 0 ? 100 : Math.Min(MaxLayers, 100000),
            CadWorksetPattern = CadWorksetPattern ?? string.Empty
        };
    }

    /// <summary>V004: one line of "Changes since last scan".</summary>
    public sealed class DwgChange
    {
        /// <summary>"New", "Removed" or "Changed".</summary>
        public string Change { get; init; } = string.Empty;

        /// <summary>File name.</summary>
        public string FileName { get; init; } = string.Empty;

        /// <summary>"Link" / "Import".</summary>
        public string Kind { get; init; } = string.Empty;

        /// <summary>What changed.</summary>
        public string Detail { get; init; } = string.Empty;
    }

    /// <summary>V004: settings remembered between sessions.</summary>
    public sealed class DwgToolSettings
    {
        /// <summary>Last quick-filter chip.</summary>
        public string QuickFilter { get; set; } = nameof(DwgQuickFilter.All);

        /// <summary>V005: rules for far-from-origin, layer count and the CAD workset check.</summary>
        public DwgRules Rules { get; set; } = new DwgRules();

        /// <summary>V005: keep the scan history next to the project file so the team shares it.</summary>
        public bool SharedHistory { get; set; } = true;

        /// <summary>Last detail tab (0 = Views, 1 = Instances, 2 = Layers).</summary>
        public int DetailTab { get; set; }

        /// <summary>Most views Open Ticked Views opens at once (1–20).</summary>
        public int MaxOpenViews { get; set; } = DwgStatisticsTool.DefaultMaxOpenViews;

        /// <summary>Close every other open view after opening the ticked ones.</summary>
        public bool CloseOtherViews { get; set; }

        /// <summary>Open the sheet a view is placed on instead of the view (when it has one).</summary>
        public bool OpenSheetInstead { get; set; }

        /// <summary>Select and zoom to the DWG in each opened view.</summary>
        public bool ZoomToDwg { get; set; } = true;

        /// <summary>Delete in Ticked Views also purges the type when no instance is left.</summary>
        public bool PurgeAfterDelete { get; set; }

        /// <summary>"Changes since last scan" card expanded.</summary>
        public bool ChangesExpanded { get; set; } = true;
    }
}
