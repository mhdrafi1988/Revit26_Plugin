// ==============================================
// File: RunSummary.cs
// Layer: Core/Models  (no Revit API — CLAUDE.md)
// Workset Creator V017 — carried from V015 via V016.
//
// Changes vs V015:
//   NEW  ExcludedIfc / ExcludedNested — links the scan deliberately left out,
//        so the report explains a missing link instead of just not listing it.
//   NEW  LinkOutcome.WasUnloaded, so a refused assignment on an unloaded link
//        reads as that rather than a generic Missed.
// Everything else — the outcome set, the derived counts, the headline and the
// plain-text report — is V015 behaviour.
//
// Added later in V016 (duplicate prevention):
//   NEW  RunOutcome.Renamed / Adopted — a link whose workset already existed
//        under another naming pattern. Renamed normalised it to the pattern
//        name; Adopted kept the old name and assigned into it. Both mean one
//        workset for one link, which is the whole point.
//   NEW  IncludedIfc, and a CompletionLine() that spells out the counts the way
//        Rafi reads them, including links deliberately left without a workset.
// ==============================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revit26_Plugin.WorksetCreator.V017.Core.Models
{
    /// <summary>What happened to one linked file in a run.</summary>
    public enum RunOutcome
    {
        /// <summary>Workset created and at least one link instance assigned to it.</summary>
        Created,

        /// <summary>Workset created, but the link has no placed instances (Grid 3) —
        /// assignment happens when someone places the link.</summary>
        CreatedNoInstances,

        /// <summary>Workset already existed; instances were assigned to it (Resync,
        /// or Create on a name that already exists).</summary>
        Reused,

        /// <summary>
        /// An existing workset matched this link under a different naming pattern and
        /// was renamed to the pattern name, then assigned. One workset, consistent name.
        /// </summary>
        Renamed,

        /// <summary>
        /// An existing workset matched this link and was kept under its own name;
        /// instances were assigned into it and nothing was created or renamed.
        /// </summary>
        Adopted,

        /// <summary>Submitted but deliberately not processed — e.g. Resync on a link
        /// whose workset does not exist yet, or the user chose Skip.</summary>
        Skipped,

        /// <summary>Workset is in place but none of the link's instances could be
        /// assigned (read-only, pinned, owned by someone else, unloaded...).</summary>
        Missed,

        /// <summary>The transaction threw; it was rolled back and the run continued.</summary>
        Error
    }

    /// <summary>One row of the run report — one linked file.</summary>
    public sealed class LinkOutcome
    {
        public string     LinkName          { get; set; }
        public string     WorksetName       { get; set; }
        public RunOutcome Outcome           { get; set; }

        /// <summary>Instances whose workset parameter was actually written.</summary>
        public int        InstancesAssigned { get; set; }

        /// <summary>Instances found in the model for this link.</summary>
        public int        InstancesTotal    { get; set; }

        /// <summary>Short reason / extra detail, shown in the card and the report.</summary>
        public string     Note              { get; set; } = string.Empty;

        /// <summary>
        /// True when a new workset was created for this link and its transaction
        /// committed — including a Missed link, whose workset exists even though no
        /// instance could be assigned to it. Set by the service, never by the UI.
        /// </summary>
        public bool       WorksetWasCreated { get; set; }

        /// <summary>The link was unloaded when the run touched it (V016).</summary>
        public bool       WasUnloaded       { get; set; }

        /// <summary>
        /// The matched workset's name before a Rename. Empty unless Outcome is
        /// Renamed — it is what makes the report show "X -> Y" rather than just Y.
        /// </summary>
        public string     RenamedFrom       { get; set; } = string.Empty;

        /// <summary>Short label used by the card pill and the report lines.</summary>
        public string OutcomeLabel => Outcome switch
        {
            RunOutcome.Created            => "Created",
            RunOutcome.CreatedNoInstances => "Created (no instances)",
            RunOutcome.Reused             => "Existing workset",
            RunOutcome.Renamed            => "Renamed + assigned",
            RunOutcome.Adopted            => "Adopted existing",
            RunOutcome.Skipped            => "Skipped",
            RunOutcome.Missed             => "Missed",
            RunOutcome.Error              => "Error",
            _                             => Outcome.ToString()
        };

        /// <summary>"3/3" — assigned out of found, for the card's instances column.</summary>
        public string InstancesText => $"{InstancesAssigned}/{InstancesTotal}";
    }

    /// <summary>
    /// Aggregate result of one Create or Resync run. Built by
    /// <c>WorksetService.CreateAndAssign</c>, held by the ViewModel as
    /// <c>LastRunSummary</c>, and rendered three ways (window card, TaskDialog,
    /// clipboard text) from these same values.
    /// </summary>
    public sealed class RunSummary
    {
        /// <summary>"Create" or "Resync".</summary>
        public string Mode { get; set; } = "Create";

        /// <summary>Which grid(s) the run came from — "Grid 2", "Grid 3", "Grid 2 &amp; 3".</summary>
        public string Label { get; set; } = string.Empty;

        public DateTime FinishedAt { get; set; } = DateTime.Now;

        /// <summary>Linked files listed by the tool when the run started (after exclusions).</summary>
        public int LinksInModel { get; set; }

        /// <summary>Linked files the user had checked for this run.</summary>
        public int LinksSelected { get; set; }

        /// <summary>IFC links the scan excluded (V016).</summary>
        public int ExcludedIfc { get; set; }

        /// <summary>Nested links the scan excluded (V016).</summary>
        public int ExcludedNested { get; set; }

        /// <summary>
        /// Listed links that came in from IFC, i.e. a ".ifc.rvt" shadow model. Such a
        /// file ends in .rvt, so it is always listed and always gets a workset; this
        /// count exists only so the report can say how many there were (V016).
        /// </summary>
        public int IncludedIfc { get; set; }

        /// <summary>
        /// Links the user chose to leave without a workset. Named rather than counted,
        /// because "every link has a workset" has to be checkable from the report.
        /// </summary>
        public List<string> LinksWithoutWorkset { get; } = new();

        public List<LinkOutcome> Outcomes { get; } = new();

        // ─── Counts (derived — never stored twice) ────────────────────────────

        /// <summary>
        /// Worksets that exist in the model because of this run. Counted from the
        /// commit, not from the outcome: a Missed link's workset was created too, and
        /// an outcome-based count reported one fewer than Revit's workset list shows.
        /// </summary>
        public int WorksetsCreated => Outcomes.Count(o => o.WorksetWasCreated);

        public int WorksetsReused => Outcomes.Count(o => o.Outcome == RunOutcome.Reused);

        /// <summary>Existing worksets renamed to the pattern name, then assigned.</summary>
        public int WorksetsRenamed => Outcomes.Count(o => o.Outcome == RunOutcome.Renamed);

        /// <summary>Existing worksets kept under their own name and assigned into.</summary>
        public int WorksetsAdopted => Outcomes.Count(o => o.Outcome == RunOutcome.Adopted);

        /// <summary>Links that ended with at least one instance assigned.</summary>
        public int LinksAssigned => Outcomes.Count(o => o.InstancesAssigned > 0);

        public int InstancesAssigned => Outcomes.Sum(o => o.InstancesAssigned);

        public int Skipped => Outcomes.Count(o => o.Outcome == RunOutcome.Skipped);

        public int Missed  => Outcomes.Count(o => o.Outcome == RunOutcome.Missed);

        public int Errors  => Outcomes.Count(o => o.Outcome == RunOutcome.Error);

        /// <summary>Unloaded links in this run that Revit refused to re-assign (V016).</summary>
        public int MissedUnloaded =>
            Outcomes.Count(o => o.Outcome == RunOutcome.Missed && o.WasUnloaded);

        public int ExcludedTotal => ExcludedIfc + ExcludedNested;

        /// <summary>True when something needs the user's attention.</summary>
        public bool HasProblems =>
            Errors > 0 || Missed > 0 || Skipped > 0 || LinksWithoutWorkset.Count > 0;

        // ─── Rendering ────────────────────────────────────────────────────────

        /// <summary>One line for the shell status strip / SummaryText.</summary>
        public string HeadlineText()
        {
            var parts = new List<string>
            {
                $"{Mode} · {WorksetsCreated} created"
            };
            if (WorksetsRenamed > 0) parts.Add($"{WorksetsRenamed} renamed");
            if (WorksetsAdopted > 0) parts.Add($"{WorksetsAdopted} adopted");
            parts.Add($"{WorksetsReused} existing");
            parts.Add($"{LinksAssigned} link(s) assigned ({InstancesAssigned} instance(s))");
            if (Skipped > 0) parts.Add($"{Skipped} skipped");
            if (Missed  > 0) parts.Add($"{Missed} missed");
            if (Errors  > 0) parts.Add($"{Errors} error(s)");
            return string.Join(" · ", parts);
        }

        /// <summary>
        /// The completion summary line, in the shape Rafi reads:
        /// "8 created | 6 renamed | 2 reused | 1 skipped (no workset) | 0 failed".
        /// Always shows every field, including the zeroes — a run that renamed nothing
        /// should say so rather than leave the reader guessing.
        /// </summary>
        public string CompletionLine()
        {
            int failed = Errors + Missed;

            string line =
                $"{WorksetsCreated} created | {WorksetsRenamed} renamed | " +
                $"{WorksetsAdopted + WorksetsReused} reused | " +
                $"{Skipped} skipped (no workset) | {failed} failed";

            if (LinksWithoutWorkset.Count > 0)
                line += $"  —  {LinksWithoutWorkset.Count} link(s) still have no workset";

            return line;
        }

        /// <summary>
        /// Full plain-text report: the counts, then one line per linked file.
        /// Used for the TaskDialog's expanded content and the Copy button.
        /// </summary>
        public string ToReportText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Workset Creator — {Mode} run ({Label})");
            sb.AppendLine($"Finished {FinishedAt:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();
            sb.AppendLine($"Linked files listed ...... {LinksInModel}");
            sb.AppendLine($"Selected for this run .... {LinksSelected}");
            sb.AppendLine($"Worksets created ......... {WorksetsCreated}");
            sb.AppendLine($"Worksets renamed ......... {WorksetsRenamed}");
            sb.AppendLine($"Existing worksets used ... {WorksetsReused + WorksetsAdopted}" +
                          (WorksetsAdopted > 0 ? $"  ({WorksetsAdopted} kept their own name)" : string.Empty));
            sb.AppendLine($"Links assigned ........... {LinksAssigned}  ({InstancesAssigned} instance(s))");
            sb.AppendLine($"Skipped .................. {Skipped}");
            sb.AppendLine($"Missed ................... {Missed}" +
                          (MissedUnloaded > 0 ? $"  ({MissedUnloaded} because the link is unloaded)" : string.Empty));
            sb.AppendLine($"Errors ................... {Errors}");

            if (ExcludedTotal > 0)
                sb.AppendLine($"Excluded by the scan ..... {ExcludedTotal}  " +
                              $"({ExcludedIfc} IFC, {ExcludedNested} nested)");

            if (IncludedIfc > 0)
                sb.AppendLine($"IFC links included ....... {IncludedIfc}");

            sb.AppendLine();
            sb.AppendLine(CompletionLine());

            if (LinksWithoutWorkset.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"Links left without a workset ({LinksWithoutWorkset.Count}):");
                foreach (string name in LinksWithoutWorkset)
                    sb.AppendLine($"    {name}");
            }

            sb.AppendLine();

            if (Outcomes.Count == 0)
            {
                sb.AppendLine("No linked files were processed.");
                return sb.ToString();
            }

            sb.AppendLine("Linked file                              Workset                        Result");
            sb.AppendLine(new string('-', 110));

            foreach (LinkOutcome o in Outcomes)
            {
                string workset = string.IsNullOrWhiteSpace(o.RenamedFrom)
                    ? o.WorksetName
                    : $"{o.RenamedFrom} -> {o.WorksetName}";

                string line =
                    $"{Trim(o.LinkName, 40),-40} {Trim(workset, 30),-30} " +
                    $"{o.OutcomeLabel,-22} {o.InstancesText,7}";
                if (!string.IsNullOrWhiteSpace(o.Note))
                    line += $"  — {o.Note}";
                sb.AppendLine(line);
            }

            return sb.ToString();
        }

        private static string Trim(string value, int max)
        {
            value ??= string.Empty;
            return value.Length <= max ? value : value[..(max - 1)] + "…";
        }
    }
}
