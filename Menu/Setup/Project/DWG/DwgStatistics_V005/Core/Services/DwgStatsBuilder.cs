using System;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin_Revised.DwgStatistics.V005.Core.Models;

namespace Revit26_Plugin_Revised.DwgStatistics.V005.Core.Services
{
    /// <summary>
    /// Turns the raw facts of one scan into per-file statistics, issue badges and project totals.
    /// Pure .NET — no Revit API — so it can be reasoned about (and tested) on its own.
    /// </summary>
    public static class DwgStatsBuilder
    {
        private const string ThreeDLabel = "3D Views";

        private const double FeetToKm = 0.3048 / 1000.0;

        /// <summary>
        /// Builds one <see cref="DwgFileStats"/> per CAD type, problems first, then by name.
        /// V005: <paramref name="rules"/> drive the far-from-origin, layer-count and workset checks
        /// (defaults when null), so rules can be re-applied to the last scan without rescanning.
        /// </summary>
        public static List<DwgFileStats> Build(DwgScanResult scan, DwgRules rules = null)
        {
            var result = new List<DwgFileStats>();
            if (scan == null) return result;
            rules = (rules ?? new DwgRules()).Normalized();
            var worksetNames = scan.Worksets.ToDictionary(w => w.Id, w => w.Name);

            var byType = scan.Instances
                .GroupBy(i => i.TypeId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // Duplicate detection: the same path (links) or the same file name brought in more than once.
            var pathCounts = scan.Types
                .Where(t => !string.IsNullOrWhiteSpace(t.Path))
                .GroupBy(t => t.Path.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
            var nameCounts = scan.Types
                .GroupBy(t => t.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            foreach (var type in scan.Types)
            {
                byType.TryGetValue(type.TypeId, out var instances);
                instances ??= new List<CadInstanceFact>();

                var instanceStats = new List<DwgInstanceStats>();
                var placed = new Dictionary<long, List<long>>();   // view → instances owned
                var visible = new Dictionary<long, List<long>>();  // view → instances drawn
                var hidden = new Dictionary<long, List<long>>();   // view → instances hidden by element
                var threeD = new HashSet<long>();                  // 3D views drawing a model instance

                foreach (var inst in instances)
                {
                    if (inst.IsViewSpecific && inst.OwnerViewId != 0)
                        Add(placed, inst.OwnerViewId, inst.Id);

                    scan.VisibleIn.TryGetValue(inst.Id, out var seenIn);
                    seenIn ??= new HashSet<long>();
                    foreach (var viewId in seenIn)
                    {
                        Add(visible, viewId, inst.Id);
                        if (!inst.IsViewSpecific && scan.Views.TryGetValue(viewId, out var vf) && vf.TypeLabel == ThreeDLabel)
                            threeD.Add(viewId);
                    }

                    scan.HiddenIn.TryGetValue(inst.Id, out var hiddenIn);
                    hiddenIn ??= new HashSet<long>();
                    foreach (var viewId in hiddenIn)
                        Add(hidden, viewId, inst.Id);

                    long best = inst.IsViewSpecific && inst.OwnerViewId != 0
                        ? inst.OwnerViewId
                        : PickBestView(seenIn, scan.Views);

                    // V005: distance (geometry extent, or insertion point when no extent) and workset rule.
                    double km = Math.Max(inst.ExtentDistanceFt, inst.OriginDistanceFt) * FeetToKm;
                    string wsName = worksetNames.TryGetValue(inst.WorksetId, out var wn) ? wn : inst.Workset;
                    // "Current view only" instances live on their view's own workset — the rule only applies
                    // to model-placed instances, which are the ones that can (and should) be moved.
                    bool wsOk = !scan.IsWorkshared || inst.IsViewSpecific || rules.IsCadWorkset(wsName);

                    instanceStats.Add(new DwgInstanceStats
                    {
                        Id = inst.Id,
                        IsViewSpecific = inst.IsViewSpecific,
                        Placement = inst.IsViewSpecific ? "View-specific" : "Model",
                        OwnerViewId = inst.OwnerViewId,
                        OwnerViewName = inst.IsViewSpecific ? ViewName(scan, inst.OwnerViewId) : "(all views)",
                        VisibleViewCount = seenIn.Count,
                        HiddenViewCount = hiddenIn.Count,
                        BestViewId = best,
                        Pinned = inst.Pinned,
                        Workset = inst.Workset,
                        Level = inst.Level,
                        CreatedBy = inst.CreatedBy,
                        DistanceKm = km,
                        WorksetOk = wsOk,
                        WorksetOkText = !scan.IsWorkshared ? "—" : inst.IsViewSpecific ? "View" : (wsOk ? "Yes" : "No"),
                        CanChangeWorkset = inst.CanChangeWorkset
                    });
                }

                // V005: the file's import category and its layers' state in each view that shows it.
                long categoryId = instances.Select(i => i.CategoryId).FirstOrDefault(c => c != 0);
                // Views tab: every view that owns, draws or hides the file.
                var shownViewIds = new HashSet<long>(placed.Keys);
                shownViewIds.UnionWith(visible.Keys);
                var allViewIds = new HashSet<long>(shownViewIds);
                allViewIds.UnionWith(hidden.Keys);

                var views = allViewIds
                    .Select(id =>
                    {
                        scan.Views.TryGetValue(id, out var vf);
                        var layerState = LayerState(scan, id, categoryId);
                        return new DwgViewUsage
                        {
                            HiddenLayers = layerState?.Hidden.Count ?? 0,
                            OverriddenLayers = layerState?.Overridden.Count ?? 0,
                            ViewId = id,
                            ViewName = vf?.Name ?? $"(view #{id})",
                            ViewType = vf?.TypeLabel ?? "Other Views",
                            Sheets = vf == null ? string.Empty : string.Join(", ", vf.SheetNumbers),
                            SheetIds = vf?.SheetIds ?? new List<long>(),
                            PlacedInstanceIds = placed.TryGetValue(id, out var p) ? p : new List<long>(),
                            VisibleInstanceIds = visible.TryGetValue(id, out var v) ? v : new List<long>(),
                            HiddenInstanceIds = hidden.TryGetValue(id, out var h) ? h : new List<long>()
                        };
                    })
                    .OrderBy(v => v.IsPlaced ? 0 : v.VisibleInstances > 0 ? 1 : 2)
                    .ThenBy(v => v.ViewType, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(v => v.ViewName, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var sheets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var id in shownViewIds)
                    if (scan.Views.TryGetValue(id, out var vf))
                        sheets.UnionWith(vf.SheetNumbers);

                var stats = new DwgFileStats
                {
                    TypeId = type.TypeId,
                    FileName = type.Name,
                    IsLinked = type.IsLinked,
                    Status = type.Status,
                    IsLoaded = type.IsLoaded,
                    Path = type.Path,
                    PathType = type.PathType,
                    FileSizeBytes = type.FileSizeBytes,
                    FileModified = type.FileModified,
                    InstanceCount = instances.Count,
                    ViewSpecificCount = instances.Count(i => i.IsViewSpecific),
                    ModelCount = instances.Count(i => !i.IsViewSpecific),
                    PinnedCount = instances.Count(i => i.Pinned),
                    PlacedViewCount = placed.Count,
                    VisibleViewCount = shownViewIds.Count,
                    HiddenViewCount = hidden.Count,
                    ThreeDViewCount = threeD.Count,
                    SheetCount = sheets.Count,
                    LayerCount = instances.Count == 0 ? 0 : instances.Max(i => i.LayerCount),
                    Instances = instanceStats.OrderBy(i => i.Placement).ThenBy(i => i.OwnerViewName).ThenBy(i => i.Id).ToList(),
                    Views = views,
                    CategoryId = categoryId,
                    MaxDistanceKm = instanceStats.Count == 0 ? 0 : instanceStats.Max(i => i.DistanceKm),
                    WrongWorksetCount = instanceStats.Count(i => !i.WorksetOk),
                    MovableWrongWorksetCount = instanceStats.Count(i => !i.WorksetOk && i.CanChangeWorkset),
                    Layers = BuildLayers(scan, categoryId, shownViewIds)
                };

                stats.Issues.AddRange(FindIssues(type, stats, pathCounts, nameCounts, rules));
                result.Add(stats);
            }

            return result
                .OrderByDescending(s => s.Issues.Count == 0 ? -1 : (int)s.Issues.Max(i => i.Severity))
                .ThenByDescending(s => s.IsLinked)
                .ThenBy(s => s.FileName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Header totals over all files.</summary>
        public static DwgProjectTotals Totals(IReadOnlyCollection<DwgFileStats> files)
        {
            var views = new HashSet<long>();
            var sheets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in files)
            {
                foreach (var v in f.Views.Where(v => v.IsPlaced || v.VisibleInstances > 0))
                {
                    views.Add(v.ViewId);
                    foreach (var s in v.Sheets.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries))
                        sheets.Add(s);
                }
            }

            return new DwgProjectTotals
            {
                Files = files.Count,
                Links = files.Count(f => f.IsLinked),
                Imports = files.Count(f => !f.IsLinked),
                Instances = files.Sum(f => f.InstanceCount),
                Views = views.Count,
                Sheets = sheets.Count,
                NotLoaded = files.Count(f => f.IsLinked && !f.IsLoaded),
                WithIssues = files.Count(f => f.HasProblem),
                Unused = files.Count(f => f.InstanceCount == 0)
            };
        }

        /// <summary>True when <paramref name="file"/> passes the quick filter.</summary>
        public static bool Passes(DwgFileStats file, DwgQuickFilter filter)
        {
            switch (filter)
            {
                case DwgQuickFilter.Links: return file.IsLinked;
                case DwgQuickFilter.Imports: return !file.IsLinked;
                case DwgQuickFilter.NotLoaded: return file.IsLinked && !file.IsLoaded;
                case DwgQuickFilter.Unused: return file.InstanceCount == 0;
                case DwgQuickFilter.Issues: return file.HasProblem;
                case DwgQuickFilter.Changed: return !string.IsNullOrEmpty(file.ChangeTag);
                default: return true;
            }
        }

        // V005: per layer, the views (among those showing the file) where it is hidden / overridden.
        private static List<DwgLayerStats> BuildLayers(DwgScanResult scan, long categoryId, HashSet<long> shownViewIds)
        {
            var list = new List<DwgLayerStats>();
            if (categoryId == 0 || !scan.LayersByCategory.TryGetValue(categoryId, out var layers)) return list;

            foreach (var layer in layers)
            {
                var hidden = new List<long>();
                var overridden = new List<long>();
                foreach (var viewId in shownViewIds)
                {
                    var state = LayerState(scan, viewId, categoryId);
                    if (state == null) continue;
                    if (state.Hidden.Contains(layer.LayerId)) hidden.Add(viewId);
                    if (state.Overridden.Contains(layer.LayerId)) overridden.Add(viewId);
                }

                var names = hidden
                    .Select(id => scan.Views.TryGetValue(id, out var v) ? v.Name : $"#{id}")
                    .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                list.Add(new DwgLayerStats
                {
                    LayerId = layer.LayerId,
                    Name = layer.Name,
                    HiddenViewIds = hidden,
                    OverriddenViewIds = overridden,
                    HiddenViewNames = names.Count <= 6
                        ? string.Join(", ", names)
                        : string.Join(", ", names.Take(6)) + $" … +{names.Count - 6}"
                });
            }

            return list.OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private static LayerViewState LayerState(DwgScanResult scan, long viewId, long categoryId)
        {
            if (categoryId == 0) return null;
            return scan.LayerStates.TryGetValue(viewId, out var byCat) && byCat.TryGetValue(categoryId, out var state)
                ? state
                : null;
        }

        private static IEnumerable<DwgIssue> FindIssues(CadTypeFact type, DwgFileStats s,
            IReadOnlyDictionary<string, int> pathCounts, IReadOnlyDictionary<string, int> nameCounts, DwgRules rules)
        {
            if (type.IsLinked && !type.IsLoaded)
                yield return new DwgIssue(type.Status, IssueSeverity.Danger,
                    $"The link is {type.Status.ToLowerInvariant()}.",
                    "Tick it and use Reload Links, or fix the path in Manage Links.");

            if (!type.IsLinked)
                yield return new DwgIssue("Imported", IssueSeverity.Warning,
                    "Imported CAD is stored inside the model and does not update.",
                    "Replace it with Link CAD (Current view only), then delete the import.");

            if (s.InstanceCount == 0)
                yield return new DwgIssue("Unused", IssueSeverity.Warning,
                    "No instance is placed anywhere.",
                    "Tick it and use Purge Unused.");

            bool duplicatePath = !string.IsNullOrWhiteSpace(type.Path)
                                 && pathCounts.TryGetValue(type.Path.Trim(), out int pc) && pc > 1;
            bool duplicateName = nameCounts.TryGetValue(type.Name.Trim(), out int nc) && nc > 1;
            if (duplicatePath || duplicateName)
                yield return new DwgIssue("Duplicate", IssueSeverity.Warning,
                    duplicatePath
                        ? "The same file is linked/imported more than once."
                        : "Another CAD type has the same file name.",
                    "Keep one; purge or delete the others.");

            if (s.ThreeDViewCount > 0)
                yield return new DwgIssue("Shows in 3D", IssueSeverity.Warning,
                    $"A model-placed instance is drawn in {s.ThreeDViewCount} 3D view(s).",
                    "Tick those 3D views and use Hide in Ticked Views, or re-link as 'Current view only'.");

            // V005 checks.
            if (s.InstanceCount > 0 && s.MaxDistanceKm > rules.FarFromOriginKm)
                yield return new DwgIssue("Far from origin", IssueSeverity.Warning,
                    $"Geometry reaches {s.MaxDistanceText} km from the internal origin (limit {rules.FarFromOriginKm:0.#} km). " +
                    "Far geometry causes graphics glitches and Revit's 'far from origin' warning.",
                    "Clean far-away objects out of the DWG, or re-link with Origin to Origin / Shared Coordinates.");

            if (s.LayerCount > rules.MaxLayers)
                yield return new DwgIssue("Many layers", IssueSeverity.Warning,
                    $"{s.LayerCount} layers (limit {rules.MaxLayers}) — every layer becomes an object style and slows Visibility/Graphics.",
                    "Purge / merge layers in the DWG and reload, or hide unneeded layers via the Layers tab.");

            if (s.WrongWorksetCount > 0)
                yield return new DwgIssue("Wrong workset", IssueSeverity.Warning,
                    $"{s.WrongWorksetCount} model-placed instance(s) are not on a CAD workset (rule: \"{rules.CadWorksetPattern}\").",
                    s.MovableWrongWorksetCount > 0
                        ? "Tick it and use Move to Workset."
                        : "Revit does not let these be moved (read-only workset parameter) — check element ownership.");

            if (s.ModelCount > 0)
                yield return new DwgIssue("Model placed", IssueSeverity.Info,
                    $"{s.ModelCount} instance(s) are not 'Current view only', so they can show in every view.",
                    "Re-link as 'Current view only' in the views that need it.");

            if (type.IsLinked && string.Equals(type.PathType, "Absolute", StringComparison.OrdinalIgnoreCase))
                yield return new DwgIssue("Absolute path", IssueSeverity.Info,
                    "The link uses an absolute path, which breaks when the project folder moves.",
                    "Manage Links → Path Type → Relative.");

            if (s.InstanceCount > 0 && s.UnpinnedCount > 0)
                yield return new DwgIssue("Unpinned", IssueSeverity.Info,
                    $"{s.UnpinnedCount} instance(s) are not pinned and can be moved by accident.",
                    "Tick it and use Pin All.");

            if (s.InstanceCount > 0 && s.VisibleViewCount > 0 && s.SheetCount == 0)
                yield return new DwgIssue("Not on sheet", IssueSeverity.Info,
                    "Shown in views, but none of them is placed on a sheet.",
                    "Expected for working views; otherwise check it is still needed.");

            if (s.InstanceCount > 0 && s.VisibleViewCount == 0)
                yield return new DwgIssue("Not visible", IssueSeverity.Info,
                    "Placed, but no view draws it (hidden by category or element, or only in views that cannot be read).",
                    "Check the Views tab for 'Hidden here' rows, or delete it if it is not needed.");
        }

        private static void Add(Dictionary<long, List<long>> map, long key, long value)
        {
            if (!map.TryGetValue(key, out var list))
            {
                list = new List<long>();
                map[key] = list;
            }
            if (!list.Contains(value)) list.Add(value);
        }

        // Prefer a plan, then other views, so Zoom opens a sensible view for a model instance.
        private static long PickBestView(HashSet<long> viewIds, IReadOnlyDictionary<long, ViewFact> views)
        {
            if (viewIds == null || viewIds.Count == 0) return 0;

            return viewIds
                .OrderBy(id => views.TryGetValue(id, out var v) ? Rank(v.TypeLabel) : 99)
                .ThenBy(id => views.TryGetValue(id, out var v) ? v.Name : string.Empty, StringComparer.OrdinalIgnoreCase)
                .First();
        }

        private static int Rank(string typeLabel)
        {
            switch (typeLabel)
            {
                case "Floor Plans": return 0;
                case "Ceiling Plans": return 1;
                case "Sections": return 2;
                case "Elevations": return 3;
                case ThreeDLabel: return 4;
                default: return 5;
            }
        }

        private static string ViewName(DwgScanResult scan, long viewId)
            => scan.Views.TryGetValue(viewId, out var v) ? v.Name : $"(view #{viewId})";
    }
}
