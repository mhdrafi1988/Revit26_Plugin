using System;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin_Revised.LinkVersionChecker_V003.Core.Models;

namespace Revit26_Plugin_Revised.LinkVersionChecker_V003.Core.Rules
{
    internal static class R
    {
        public static IEnumerable<Finding> None => Enumerable.Empty<Finding>();
        public static IEnumerable<Finding> One(Finding f) { yield return f; }

        public static int YearsBehind(string year, int current) =>
            int.TryParse(year, out int y) ? Math.Max(1, current - y) : 1;
    }

    // ------------------------------------------------------------------ 1
    public sealed class CloudLinkRule : ILinkRule
    {
        public string Id => "CLOUD";
        public IEnumerable<Finding> Evaluate(LinkInfo l, RuleContext c) => !l.IsCloud ? R.None : R.One(new Finding(Id, Severity.Info,
            "Cloud model",
            "Cloud (ACC / BIM 360) model — its version can't be read from disk. Check it in ACC Docs; older cloud models must be upgraded by their owner."));
    }

    // ------------------------------------------------------------------ 2
    public sealed class MissingLinkRule : ILinkRule
    {
        public string Id => "MISSING";
        public IEnumerable<Finding> Evaluate(LinkInfo l, RuleContext c)
        {
            if (l.Exists || l.IsCloud || l.IsImport) return R.None;
            return R.One(new Finding(Id, Severity.Critical, "Not found",
                "File not found at its saved path. Revit searches for it on every open. Repath it or remove the link.",
                (int)LoadCostModel.MissingLinkSeconds));
        }
    }

    // ------------------------------------------------------------------ 3
    public sealed class UnreadableHeaderRule : ILinkRule
    {
        public string Id => "UNREADABLE";
        public IEnumerable<Finding> Evaluate(LinkInfo l, RuleContext c)
        {
            if (l.Kind != LinkKind.RVT || !l.Exists || l.HeaderRead || l.IsCloud) return R.None;
            return R.One(new Finding(Id, Severity.Warning, "Unreadable",
                "Could not read the file header" + (string.IsNullOrEmpty(l.HeaderError) ? "." : ": " + l.HeaderError)));
        }
    }

    // ------------------------------------------------------------------ 4
    public sealed class LaterVersionRule : ILinkRule
    {
        public string Id => "LATER_VERSION";
        public IEnumerable<Finding> Evaluate(LinkInfo l, RuleContext c)
        {
            if (l.Kind != LinkKind.RVT || !l.HeaderRead || !l.IsLaterVersion) return R.None;
            return R.One(new Finding(Id, Severity.Critical, "Newer version",
                $"Saved in {l.SavedYear}, newer than Revit {c.CurrentYear}. It cannot load — ask for a {c.CurrentYear} copy."));
        }
    }

    // ------------------------------------------------------------------ 5
    public sealed class TemporaryUpgradeRule : ILinkRule
    {
        public string Id => "TEMP_UPGRADE";
        public IEnumerable<Finding> Evaluate(LinkInfo l, RuleContext c)
        {
            if (l.Kind != LinkKind.RVT || !l.HeaderRead || l.IsCurrentVersion || l.IsLaterVersion) return R.None;

            int behind = R.YearsBehind(l.SavedYear, c.CurrentYear);
            int est = (int)Math.Round(LoadCostModel.UpgradeBaseSeconds
                                      + l.SizeMb * LoadCostModel.UpgradeSecondsPerMb
                                      + LoadCostModel.UpgradeSecondsPerExtraVersion * (behind - 1));
            string msg = l.IsWorkshared
                ? $"Saved in {l.SavedYear} → upgraded in memory every time the host opens (never saved). Workshared ({l.WorksharedText}) — ask the model owner to upgrade the central to {c.CurrentYear}."
                : $"Saved in {l.SavedYear} → upgraded in memory every time the host opens. Use Upgrade & Save (a backup is kept) so it opens directly in {c.CurrentYear}.";
            return R.One(new Finding(Id, Severity.Critical, "Temporary upgrade", msg, est));
        }
    }

    // ------------------------------------------------------------------ 6
    public sealed class IfcCacheRule : ILinkRule
    {
        public string Id => "IFC_CACHE";
        public IEnumerable<Finding> Evaluate(LinkInfo l, RuleContext c)
        {
            if (l.Kind != LinkKind.IFC || l.IsCloud || !l.Exists) return R.None;

            int est = (int)Math.Round(LoadCostModel.IfcBaseSeconds + l.SizeMb * LoadCostModel.IfcSecondsPerMb);

            if (!l.CacheExists)
            {
                string msg = l.CacheFolderWritable
                    ? "No .ifc.RVT cache, so the IFC is converted on every open. Use Rebuild IFC cache (In-Model mode reloads it once to create the cache)."
                    : "No .ifc.RVT cache and the IFC folder is read-only, so Revit can't save one. Copy the IFC to a writable folder and repath.";
                return R.One(new Finding(Id, Severity.Warning, "Cache missing", msg, est));
            }
            if (!l.CacheIsCurrent)
                return R.One(new Finding(Id, Severity.Warning, "Cache stale",
                    $"The .ifc.RVT cache was built by Revit {l.CacheYear}, so it is upgraded on every open. Rebuild the cache once in {c.CurrentYear}.", est));
            if (l.CacheIsOlderThanIfc)
                return R.One(new Finding(Id, Severity.Warning, "Cache stale",
                    $"The .ifc.RVT cache ({l.CacheTimeUtc?.ToLocalTime():dd-MMM-yy HH:mm}) is older than the IFC ({l.IfcTimeUtc?.ToLocalTime():dd-MMM-yy HH:mm}), so the IFC is reconverted. Rebuild the cache.", est));
            return R.None;
        }
    }

    // ------------------------------------------------------------------ 7
    public sealed class DuplicateLinkRule : ILinkRule
    {
        public string Id => "DUPLICATE";
        public IEnumerable<Finding> Evaluate(LinkInfo l, RuleContext c)
        {
            if (l.IsNested || l.IsImport || string.IsNullOrEmpty(l.FullPath)) return R.None;
            var same = c.HostLinks.Where(o => !o.IsNested && !o.IsImport &&
                                              string.Equals(o.FullPath, l.FullPath, StringComparison.OrdinalIgnoreCase)).ToList();
            if (same.Count < 2) return R.None;

            // Only the 2nd, 3rd... copy carries the cost.
            bool isFirst = ReferenceEquals(same[0], l);
            int est = isFirst ? 0 : (int)Math.Round(5 + l.SizeMb * LoadCostModel.DuplicateSecondsPerMb);
            return R.One(new Finding(Id, Severity.Warning, "Duplicate link",
                $"This file is linked {same.Count} times in the host, so it loads {same.Count} times. Keep one link type and copy its instances instead.", est));
        }
    }

    // ------------------------------------------------------------------ 8
    public sealed class ImportedCadRule : ILinkRule
    {
        public string Id => "IMPORTED_CAD";
        public IEnumerable<Finding> Evaluate(LinkInfo l, RuleContext c)
        {
            if (!l.IsImport) return R.None;
            return R.One(new Finding(Id, Severity.Warning, "Imported CAD",
                $"Imported, not linked ({Math.Max(l.Instances, 1)} instance(s)). Imports are embedded in the host, bloat the file and slow every open. Delete it, or link the CAD instead."));
        }
    }

    // ------------------------------------------------------------------ 9
    public sealed class LoadedPointCloudRule : ILinkRule
    {
        public string Id => "POINT_CLOUD";
        public IEnumerable<Finding> Evaluate(LinkInfo l, RuleContext c)
        {
            if (l.Kind != LinkKind.PointCloud || l.IsUnloaded || !l.Exists) return R.None;
            bool big = l.SizeMb >= 1024;
            return R.One(new Finding(Id, big ? Severity.Warning : Severity.Info, "Point cloud loaded",
                $"Point cloud is loaded ({l.SizeText}). Unload it when you don't need it — it costs memory and view regeneration time."));
        }
    }

    // ------------------------------------------------------------------ 10
    public sealed class AllWorksetsOpenRule : ILinkRule
    {
        public string Id => "WORKSETS";
        public IEnumerable<Finding> Evaluate(LinkInfo l, RuleContext c)
        {
            if (l.WorksetsTotal < 4 || l.WorksetsOpen < l.WorksetsTotal || l.SizeMb < 100) return R.None;
            return R.One(new Finding(Id, Severity.Info, "All worksets open",
                $"All {l.WorksetsTotal} worksets of this workshared link are open. Close the ones you don't need (Manage Links → Manage Worksets) to load less."));
        }
    }

    // ------------------------------------------------------------------ 11
    public sealed class NestedLinkRule : ILinkRule
    {
        public string Id => "NESTED";
        public IEnumerable<Finding> Evaluate(LinkInfo l, RuleContext c)
        {
            if (!l.IsNested) return R.None;
            string msg = c.InModel
                ? (l.IsUnloaded
                    ? $"Nested link of {l.ParentName} — currently not loaded."
                    : $"Nested link of {l.ParentName}, loaded because its parent attaches it as {l.Attachment}. If you don't need it, ask the parent's owner to change it to Overlay.")
                : $"Nested inside {l.ParentName}. It only loads if {l.ParentName} attaches it as Attachment (Overlay links don't load) — its estimate is shown in brackets and left out of the total.";
            return R.One(new Finding(Id, Severity.Info, "Nested", msg));
        }
    }

    // ------------------------------------------------------------------ 12
    public sealed class AbsolutePathRule : ILinkRule
    {
        public string Id => "ABS_PATH";
        public IEnumerable<Finding> Evaluate(LinkInfo l, RuleContext c)
        {
            if (l.IsCloud || l.IsImport || l.IsNested || !l.Exists) return R.None;
            if (!string.Equals(l.PathType, "Absolute", StringComparison.OrdinalIgnoreCase)) return R.None;
            return R.One(new Finding(Id, Severity.Info, "Absolute path",
                l.IsUnc
                    ? "Uses an absolute network path. A relative path survives folder moves and project copies."
                    : "Uses an absolute local path — it breaks on any other computer. Use a relative path."));
        }
    }
}
