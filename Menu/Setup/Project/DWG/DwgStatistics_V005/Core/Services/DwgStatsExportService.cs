using System;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin_Revised.DwgStatistics.V005.Core.Models;

namespace Revit26_Plugin_Revised.DwgStatistics.V005.Core.Services
{
    /// <summary>Values the export needs besides the file list.</summary>
    public sealed class DwgExportContext
    {
        /// <summary>Project title.</summary>
        public string ProjectTitle { get; init; } = string.Empty;

        /// <summary>Header totals.</summary>
        public DwgProjectTotals Totals { get; init; } = new DwgProjectTotals();

        /// <summary>Views checked by the deep scan.</summary>
        public int ViewsScanned { get; init; }

        /// <summary>Views skipped by the deep scan.</summary>
        public int ViewsSkipped { get; init; }

        /// <summary>Scan duration.</summary>
        public TimeSpan Elapsed { get; init; }

        /// <summary>V004: changes since the baseline scan.</summary>
        public IReadOnlyList<DwgChange> Changes { get; init; } = new List<DwgChange>();

        /// <summary>V004: when the baseline scan ran, or null when there was none.</summary>
        public DateTime? BaselineScannedAt { get; init; }

        /// <summary>V005: who ran the baseline scan.</summary>
        public string BaselineScannedBy { get; init; } = string.Empty;

        /// <summary>V005: recent scans (shared history log), newest first.</summary>
        public IReadOnlyList<DwgHistoryEntry> History { get; init; } = new List<DwgHistoryEntry>();

        /// <summary>V005: where the history lives ("shared (…)" / "this computer").</summary>
        public string HistoryLocation { get; init; } = string.Empty;

        /// <summary>V005: rules in force.</summary>
        public DwgRules Rules { get; init; } = new DwgRules();
    }

    /// <summary>
    /// Writes the statistics to an .xlsx workbook: Summary, Issues &amp; Fixes, Views, Instances,
    /// Layers (V005), Changes, Scan History (V005) and Scan Info. Pure .NET; runs on the UI thread from the last scan, so it never
    /// needs Revit.
    /// </summary>
    public static class DwgStatsExportService
    {
        /// <summary>Saves the workbook to <paramref name="path"/>.</summary>
        public static void Export(string path, IReadOnlyList<DwgFileStats> files, DwgExportContext ctx)
        {
            var xlsx = new SimpleXlsxWriter();

            xlsx.AddSheet("Summary",
                new[]
                {
                    "DWG File", "Kind", "Status", "Since Last Scan", "Instances", "View-specific", "Model", "Pinned", "Unpinned",
                    "Placed in Views", "Views Total (placed + visible)", "3D Views", "Hidden in Views", "Sheets", "Layers",
                    "Max Distance (km)", "Wrong Workset", "Updated on Disk",
                    "Issues", "Path", "Path Type", "File Size (KB)", "File Modified", "Type Id"
                },
                files.Select(f => new object[]
                {
                    f.FileName, f.Kind, f.Status, f.ChangeTag, f.InstanceCount, f.ViewSpecificCount, f.ModelCount,
                    f.PinnedCount, f.UnpinnedCount, f.PlacedViewCount, f.VisibleViewCount, f.ThreeDViewCount,
                    f.HiddenViewCount, f.SheetCount, f.LayerCount,
                    f.InstanceCount == 0 ? (object)null : Math.Round(f.MaxDistanceKm, 3), f.WrongWorksetCount,
                    f.IsUpdatedOnDisk ? "Yes" : string.Empty,
                    f.IssueText, f.Path, f.PathType,
                    f.FileSizeBytes.HasValue ? Math.Round(f.FileSizeBytes.Value / 1024.0, 1) : (object)null,
                    f.FileModified, f.TypeId
                }));

            xlsx.AddSheet("Issues & Fixes",
                new[] { "Severity", "DWG File", "Kind", "Issue", "What it means", "How to fix", "Type Id" },
                files.SelectMany(f => f.Issues.Select(i => new object[]
                    {
                        i.SeverityName, f.FileName, f.Kind, i.Text, i.Detail, i.Fix, f.TypeId
                    }))
                    .OrderBy(r => SeverityRank((string)r[0]))
                    .ThenBy(r => (string)r[1], StringComparer.OrdinalIgnoreCase));

            xlsx.AddSheet("Views",
                new[] { "DWG File", "Kind", "View", "View Type", "Relation", "Placed Instances", "Visible Instances", "Hidden Instances",
                        "Hidden Layers", "Overridden Layers", "Sheets", "View Id" },
                files.SelectMany(f => f.Views.Select(v => new object[]
                {
                    f.FileName, f.Kind, v.ViewName, v.ViewType, v.Relation, v.PlacedInstances, v.VisibleInstances, v.HiddenInstances,
                    v.HiddenLayers, v.OverriddenLayers, v.Sheets, v.ViewId
                })));

            xlsx.AddSheet("Instances",
                new[] { "DWG File", "Kind", "Instance Id", "Placement", "Owner View", "Visible in Views", "Hidden in Views", "Pinned",
                        "Distance (km)", "Workset", "Workset OK", "Level", "Created By" },
                files.SelectMany(f => f.Instances.Select(i => new object[]
                {
                    f.FileName, f.Kind, i.Id, i.Placement, i.OwnerViewName, i.VisibleViewCount, i.HiddenViewCount, i.PinnedText,
                    Math.Round(i.DistanceKm, 3), i.Workset, i.WorksetOkText, i.Level, i.CreatedBy
                })));

            xlsx.AddSheet("Layers",
                new[] { "DWG File", "Kind", "Layer", "Hidden in Views", "Overridden in Views", "Hidden in (views)", "Layer Id" },
                files.SelectMany(f => f.Layers.Select(l => new object[]
                {
                    f.FileName, f.Kind, l.Name, l.HiddenInViews, l.OverriddenInViews, l.HiddenViewNames, l.LayerId
                })));

            xlsx.AddSheet("Changes",
                new[] { "Change", "DWG File", "Kind", "Detail" },
                ctx.Changes.Count > 0
                    ? ctx.Changes.Select(c => new object[] { c.Change, c.FileName, c.Kind, c.Detail })
                    : new List<object[]>
                    {
                        new object[] { ctx.BaselineScannedAt.HasValue ? "No changes" : "First scan", string.Empty, string.Empty,
                            ctx.BaselineScannedAt.HasValue
                                ? $"Nothing changed since the scan of {ctx.BaselineScannedAt:yyyy-MM-dd HH:mm}."
                                : "There was no earlier scan of this project to compare with." }
                    });

            xlsx.AddSheet("Scan History",
                new[] { "Scanned At", "Scanned By", "Tool", "DWG Files", "Instances", "Files with Issues", "Changes" },
                ctx.History.Count > 0
                    ? ctx.History.Select(h => new object[] { h.ScannedAt, h.ScannedBy, h.Tool, h.Files, h.Instances, h.WithIssues, h.Changes })
                    : new List<object[]> { new object[] { "No earlier scans recorded." } });

            var t = ctx.Totals;
            xlsx.AddSheet("Scan Info",
                new[] { "Item", "Value" },
                new List<object[]>
                {
                    new object[] { "Project", ctx.ProjectTitle },
                    new object[] { "Exported", DateTime.Now },
                    new object[] { "Tool", DwgStatisticsTool.Label },
                    new object[] { "DWG files", t.Files },
                    new object[] { "Links", t.Links },
                    new object[] { "Imports", t.Imports },
                    new object[] { "Instances", t.Instances },
                    new object[] { "Views showing CAD", t.Views },
                    new object[] { "Sheets showing CAD", t.Sheets },
                    new object[] { "Links not loaded", t.NotLoaded },
                    new object[] { "Unused types", t.Unused },
                    new object[] { "Files with issues", t.WithIssues },
                    new object[] { "Views scanned", ctx.ViewsScanned },
                    new object[] { "Views skipped", ctx.ViewsSkipped },
                    new object[] { "Scan time (s)", Math.Round(ctx.Elapsed.TotalSeconds, 1) },
                    new object[] { "Compared with scan of", ctx.BaselineScannedAt.HasValue ? (object)ctx.BaselineScannedAt.Value : "— (first scan)" },
                    new object[] { "Changes since then", ctx.Changes.Count },
                    new object[] { "Baseline scanned by", ctx.BaselineScannedBy },
                    new object[] { "History location", ctx.HistoryLocation },
                    new object[] { "Rule: far from origin (km)", ctx.Rules.FarFromOriginKm },
                    new object[] { "Rule: max layers", ctx.Rules.MaxLayers },
                    new object[] { "Rule: CAD worksets", ctx.Rules.CadWorksetPattern }
                });

            xlsx.Save(path);
        }

        /// <summary>"Project_DWG_Statistics_20261013_0945.xlsx" with characters Windows rejects removed.</summary>
        public static string DefaultFileName(string projectTitle)
        {
            string name = string.IsNullOrWhiteSpace(projectTitle) ? "Project" : projectTitle;
            // Windows-invalid characters listed explicitly too, so the name is safe whatever OS builds it.
            foreach (char c in global::System.IO.Path.GetInvalidFileNameChars().Concat("<>:\"/\\|?*"))
                name = name.Replace(c, '_');
            name = name.Trim().TrimEnd('.');
            return $"{name}_DWG_Statistics_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        }

        private static int SeverityRank(string severity)
            => severity == nameof(IssueSeverity.Danger) ? 0 : severity == nameof(IssueSeverity.Warning) ? 1 : 2;
    }
}
