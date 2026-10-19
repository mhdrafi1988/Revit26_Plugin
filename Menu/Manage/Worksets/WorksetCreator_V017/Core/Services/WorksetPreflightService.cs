// ==============================================
// File: WorksetPreflightService.cs
// Layer: Core/Services  (no Revit API, no UI — CLAUDE.md)
// Workset Creator V017 — this file was introduced in V016.
//
// The guarantee, enforced. Rafi's two hard rules are:
//   - one workset per linked file, never more
//   - every linked file gets one
//
// Trusting the matcher to deliver that is not the same as checking it, so this
// runs over the finished plan BEFORE the TransactionGroup opens. A non-empty
// conflict list blocks the run outright: the dialog lists what is wrong and
// nothing is committed. A clean list is the only way into the transaction.
//
// Four checks, in the order a user would read them:
//   1. Two selected links would land in one workset.
//   2. A proposed name is already held by a workset matched to a different link.
//   3. A selected row still needs the user's decision.
//   4. A selected row has no target workset name at all.
//
// Skip is NOT a conflict — the user is allowed to leave a link out. It is
// reported instead, so "every link has a workset" stays verifiable from the
// summary rather than assumed.
// ==============================================

using Revit26_Plugin_Revised.WorksetCreator.V017.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revit26_Plugin_Revised.WorksetCreator.V017.Core.Services
{
    public enum PreflightConflictKind
    {
        /// <summary>Two or more selected links resolve to the same workset name.</summary>
        SharedTarget,

        /// <summary>The proposed name is an existing workset that belongs to another link.</summary>
        NameOwnedByOtherLink,

        /// <summary>The row is selected but its action is still unresolved.</summary>
        UnresolvedReview,

        /// <summary>The row is selected but has no workset name to act on.</summary>
        MissingTarget
    }

    public sealed class PreflightConflict
    {
        public PreflightConflictKind Kind { get; set; }

        /// <summary>Heading shown above the detail line in the dialog.</summary>
        public string Title { get; set; } = string.Empty;

        /// <summary>The offending links / worksets, as one readable line.</summary>
        public string Detail { get; set; } = string.Empty;

        /// <summary>Link type ids involved — lets the UI select the rows later if wanted.</summary>
        public List<int> LinkTypeIds { get; } = new();
    }

    public sealed class PreflightResult
    {
        public List<PreflightConflict> Conflicts { get; } = new();

        /// <summary>Selected rows the user chose to skip. Reported, not blocked.</summary>
        public List<string> SkippedLinkNames { get; } = new();

        /// <summary>Links in the whole model that will still have no workset after this run.</summary>
        public List<string> LinksWithoutWorkset { get; } = new();

        public bool CanRun => Conflicts.Count == 0;

        /// <summary>Plain text for the dialog's Copy button and the log.</summary>
        public string ToReportText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Workset Creator — pre-flight check");
            sb.AppendLine($"Checked {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            if (Conflicts.Count == 0)
            {
                sb.AppendLine("No conflicts — one workset per link.");
            }
            else
            {
                sb.AppendLine($"{Conflicts.Count} conflict(s) — nothing was changed.");
                sb.AppendLine();
                foreach (PreflightConflict conflict in Conflicts)
                {
                    sb.AppendLine(conflict.Title);
                    sb.AppendLine($"    {conflict.Detail}");
                    sb.AppendLine();
                }
            }

            if (SkippedLinkNames.Count > 0)
            {
                sb.AppendLine($"Skipped by choice ({SkippedLinkNames.Count}):");
                foreach (string name in SkippedLinkNames) sb.AppendLine($"    {name}");
                sb.AppendLine();
            }

            if (LinksWithoutWorkset.Count > 0)
            {
                sb.AppendLine($"Links that will still have no workset ({LinksWithoutWorkset.Count}):");
                foreach (string name in LinksWithoutWorkset) sb.AppendLine($"    {name}");
            }

            return sb.ToString();
        }
    }

    public sealed class WorksetPreflightService
    {
        /// <summary>
        /// Checks the plan. <paramref name="selected"/> is what the run would submit;
        /// <paramref name="allItems"/> is every row in the window, used only to report
        /// which links are left without a workset. <paramref name="existingWorksets"/>
        /// is the model's current workset list.
        /// </summary>
        public PreflightResult Check(
            IEnumerable<WorksetItem> selected,
            IEnumerable<WorksetItem> allItems,
            IEnumerable<WorksetInfo> existingWorksets)
        {
            var result = new PreflightResult();

            List<WorksetItem> rows = selected?.Where(i => i != null).ToList() ?? new List<WorksetItem>();
            List<WorksetItem> all  = allItems?.Where(i => i != null).ToList() ?? rows;
            List<WorksetInfo> worksets =
                existingWorksets?.Where(w => w != null).ToList() ?? new List<WorksetInfo>();

            // ── Skips and unresolved rows ────────────────────────────────────
            foreach (WorksetItem row in rows)
            {
                if (row.SelectedAction == WorksetAction.Skip)
                {
                    result.SkippedLinkNames.Add(row.LinkName);
                    continue;
                }

                if (row.NeedsReview && !row.IsReviewResolved)
                {
                    var conflict = new PreflightConflict
                    {
                        Kind   = PreflightConflictKind.UnresolvedReview,
                        Title  = "Row still needs your decision",
                        Detail = $"{row.LinkName} — {row.MatchExplanation}"
                    };
                    conflict.LinkTypeIds.Add(row.LinkTypeId);
                    result.Conflicts.Add(conflict);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(row.FinalWorksetName))
                {
                    var conflict = new PreflightConflict
                    {
                        Kind   = PreflightConflictKind.MissingTarget,
                        Title  = "Row has no workset name",
                        Detail = $"{row.LinkName} — nothing to create, rename or assign into"
                    };
                    conflict.LinkTypeIds.Add(row.LinkTypeId);
                    result.Conflicts.Add(conflict);
                }
            }

            // ── Check 1 — two links, one workset ─────────────────────────────
            var actionable = rows
                .Where(r => r.SelectedAction != WorksetAction.Skip &&
                            !string.IsNullOrWhiteSpace(r.FinalWorksetName) &&
                            (!r.NeedsReview || r.IsReviewResolved))
                .ToList();

            foreach (var group in actionable
                         .GroupBy(r => r.FinalWorksetName, StringComparer.OrdinalIgnoreCase)
                         .Where(g => g.Count() > 1))
            {
                var conflict = new PreflightConflict
                {
                    Kind   = PreflightConflictKind.SharedTarget,
                    Title  = "Two links would land in one workset",
                    Detail = $"{string.Join("  +  ", group.Select(r => r.LinkName))}  ->  {group.Key}"
                };
                foreach (WorksetItem row in group) conflict.LinkTypeIds.Add(row.LinkTypeId);
                result.Conflicts.Add(conflict);
            }

            // ── Check 2 — the name already belongs to another link ───────────
            // A Create or Rename onto a name an existing workset holds is only safe
            // when that workset is this link's own match. Otherwise the run would
            // merge two links' worksets, or Workset.Create would collide.
            var worksetByName = new Dictionary<string, WorksetInfo>(StringComparer.OrdinalIgnoreCase);
            foreach (WorksetInfo workset in worksets)
                worksetByName[workset.Name] = workset;

            // Keyed on the workset each row has CHOSEN to act on, not on its best
            // match — on an ambiguous row those differ, and the choice is what runs.
            var ownerByWorksetId = actionable
                .Where(r => EffectiveWorksetId(r) != 0)
                .GroupBy(EffectiveWorksetId)
                .ToDictionary(g => g.Key, g => g.First());

            foreach (WorksetItem row in actionable)
            {
                if (row.SelectedAction != WorksetAction.Create &&
                    row.SelectedAction != WorksetAction.Rename) continue;

                if (!worksetByName.TryGetValue(row.FinalWorksetName, out WorksetInfo target)) continue;

                // Renaming a workset to the name it already has is a no-op, not a clash.
                if (target.Id == EffectiveWorksetId(row)) continue;

                if (ownerByWorksetId.TryGetValue(target.Id, out WorksetItem owner) &&
                    owner.LinkTypeId != row.LinkTypeId)
                {
                    var conflict = new PreflightConflict
                    {
                        Kind   = PreflightConflictKind.NameOwnedByOtherLink,
                        Title  = "Proposed name already belongs to another link",
                        Detail = $"{row.LinkName} -> {row.FinalWorksetName}  ·  held by {owner.LinkName}"
                    };
                    conflict.LinkTypeIds.Add(row.LinkTypeId);
                    conflict.LinkTypeIds.Add(owner.LinkTypeId);
                    result.Conflicts.Add(conflict);
                    continue;
                }

                // An unmatched existing workset of the same name is fine — the service
                // re-reads and re-uses it rather than creating a second one. Logged by
                // the service at run time, not a conflict here.
            }

            // ── Check 4 — what is left without a workset ─────────────────────
            var covered = new HashSet<int>(actionable.Select(r => r.LinkTypeId));

            foreach (WorksetItem row in all)
            {
                if (covered.Contains(row.LinkTypeId)) continue;
                if (row.GridCategory == WorksetGridCategory.AlreadyAssigned) continue;
                result.LinksWithoutWorkset.Add(row.LinkName);
            }

            // ── Check 3 — two rows fighting over one existing workset ───────
            // Distinct from Check 1: two links can choose DIFFERENT final names while
            // both renaming the same existing workset, which would leave one of them
            // with no workset at all.
            foreach (var group in actionable
                         .Where(r => EffectiveWorksetId(r) != 0)
                         .GroupBy(EffectiveWorksetId)
                         .Where(g => g.Count() > 1))
            {
                var conflict = new PreflightConflict
                {
                    Kind   = PreflightConflictKind.SharedTarget,
                    Title  = "Two links would act on the same existing workset",
                    Detail = $"{string.Join("  +  ", group.Select(r => r.LinkName))}  ->  " +
                             $"{group.First().SelectedTargetName}"
                };
                foreach (WorksetItem row in group) conflict.LinkTypeIds.Add(row.LinkTypeId);
                result.Conflicts.Add(conflict);
            }

            return result;
        }

        /// <summary>
        /// The workset a row will actually act on: the option the user chose, falling
        /// back to the matcher's best candidate. 0 when the row creates or skips.
        /// </summary>
        private static int EffectiveWorksetId(WorksetItem row) =>
            row.SelectedTargetWorksetId != 0 ? row.SelectedTargetWorksetId : row.MatchedWorksetId;
    }
}
