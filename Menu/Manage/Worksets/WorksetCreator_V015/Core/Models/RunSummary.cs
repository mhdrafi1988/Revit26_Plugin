// ==============================================
// File: RunSummary.cs
// Layer: Core/Models
// NEW in Workset Creator V015 (renamed lineage of Workset Manager V014).
//
// The run report the tool shows after every Create / Resync run: the counts
// the user asked for (linked files, created worksets, skipped, missed,
// errors) plus one row per link, so the window card, the TaskDialog and the
// clipboard text all read from the same object instead of re-deriving
// anything from the Live Log.
//
// No Revit API dependency (CLAUDE.md — Core/Services and Core/Models stay
// pure); the service fills it in while it runs.
// ==============================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revit26_Plugin_Revised.WorksetCreator.V015.Core.Models
{
    /// <summary>What happened to one linked file in a run.</summary>
    public enum RunOutcome
    {
        /// <summary>Workset created and at least one link instance assigned to it.</summary>
        Created,

        /// <summary>Workset created, but the link has no placed instances (Grid 3) —
        /// assignment happens when the link is loaded.</summary>
        CreatedNoInstances,

        /// <summary>Workset already existed; instances were assigned to it (Resync,
        /// or Create on a name that already exists).</summary>
        Reused,

        /// <summary>Submitted but deliberately not processed — e.g. Resync on a link
        /// whose workset does not exist yet.</summary>
        Skipped,

        /// <summary>Workset is in place but none of the link's instances could be
        /// assigned (no ELEM_PARTITION_PARAM, read-only, pinned...).</summary>
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

        /// <summary>Short label used by the card pill and the report lines.</summary>
        public string OutcomeLabel => Outcome switch
        {
            RunOutcome.Created            => "Created",
            RunOutcome.CreatedNoInstances => "Created (no instances)",
            RunOutcome.Reused             => "Existing workset",
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

        /// <summary>Linked files found in the model when the run started.</summary>
        public int LinksInModel { get; set; }

        /// <summary>Linked files the user had checked for this run.</summary>
        public int LinksSelected { get; set; }

        public List<LinkOutcome> Outcomes { get; } = new();

        // ─── Counts (derived — never stored twice) ────────────────────────────

        /// <summary>
        /// Worksets that exist in the model because of this run. Counted from the
        /// commit, not from the outcome: a Missed link's workset was created too, and
        /// an outcome-based count reported one fewer than Revit's workset list shows.
        /// </summary>
        public int WorksetsCreated => Outcomes.Count(o => o.WorksetWasCreated);

        public int WorksetsReused => Outcomes.Count(o => o.Outcome == RunOutcome.Reused);

        /// <summary>Links that ended with at least one instance assigned.</summary>
        public int LinksAssigned => Outcomes.Count(o => o.InstancesAssigned > 0);

        public int InstancesAssigned => Outcomes.Sum(o => o.InstancesAssigned);

        public int Skipped => Outcomes.Count(o => o.Outcome == RunOutcome.Skipped);

        public int Missed  => Outcomes.Count(o => o.Outcome == RunOutcome.Missed);

        public int Errors  => Outcomes.Count(o => o.Outcome == RunOutcome.Error);

        /// <summary>True when something needs the user's attention.</summary>
        public bool HasProblems => Errors > 0 || Missed > 0 || Skipped > 0;

        // ─── Rendering ────────────────────────────────────────────────────────

        /// <summary>One line for the shell status strip / SummaryText.</summary>
        public string HeadlineText()
        {
            var parts = new List<string>
            {
                $"{Mode} · {WorksetsCreated} created",
                $"{WorksetsReused} existing",
                $"{LinksAssigned} link(s) assigned ({InstancesAssigned} instance(s))"
            };
            if (Skipped > 0) parts.Add($"{Skipped} skipped");
            if (Missed  > 0) parts.Add($"{Missed} missed");
            if (Errors  > 0) parts.Add($"{Errors} error(s)");
            return string.Join(" · ", parts);
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
            sb.AppendLine($"Linked files in model .... {LinksInModel}");
            sb.AppendLine($"Selected for this run .... {LinksSelected}");
            sb.AppendLine($"Worksets created ......... {WorksetsCreated}");
            sb.AppendLine($"Existing worksets used ... {WorksetsReused}");
            sb.AppendLine($"Links assigned ........... {LinksAssigned}  ({InstancesAssigned} instance(s))");
            sb.AppendLine($"Skipped .................. {Skipped}");
            sb.AppendLine($"Missed ................... {Missed}");
            sb.AppendLine($"Errors ................... {Errors}");
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
                string line =
                    $"{Trim(o.LinkName, 40),-40} {Trim(o.WorksetName, 30),-30} " +
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
