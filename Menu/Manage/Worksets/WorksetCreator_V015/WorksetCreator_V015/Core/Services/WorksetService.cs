// ==============================================
// File: WorksetService.cs
// Layer: Core/Services  (no Revit document mutation outside a Transaction;
//         no UI / ViewModel dependency — CLAUDE.md)
// Workset Creator V015 — renamed lineage of Workset Manager V014.
//
// Changes vs WorksetManager V014:
//   NEW  CreateAndAssign fills and returns a RunSummary (one LinkOutcome per
//        linked file) so the window card, the TaskDialog and the clipboard
//        report all read the same counts. Log text is unchanged.
//   NEW  The whole run sits in one TransactionGroup, Assimilate()d when anything
//        committed — Revit's undo stack gets one entry per run instead of one per
//        link (ARCHITECTURE.md §5). Per-link rollback behaviour is unchanged.
//   FIX  AssignLinkInstances returned instances.Count even when
//        ELEM_PARTITION_PARAM was null and nothing was written, so a link
//        whose instances are pinned / read-only was reported as assigned.
//        It now counts only the parameters actually set and reports the rest.
//   CHG  The WorksetsViewModel parameter is gone (a Core service must not
//        hold a ViewModel); progress is reported through an Action<int,int>
//        callback instead — the caller decides what to do with it.
// Everything else (queries, name cleaning, duplicate resolution, one
// Transaction per link, roll-back-and-continue) is V014 behaviour.
// ==============================================

using Autodesk.Revit.DB;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.WorksetCreator.V015.Core.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Revit26_Plugin.WorksetCreator.V015.Core.Services
{
    public class WorksetService
    {
        private readonly Action<LogEntry> _log;

        public WorksetService(Action<LogEntry> logger) => _log = logger;

        // ─── Queries ──────────────────────────────────────────────────────────

        public List<string> GetLinkedFileNames(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkType))
                .Cast<RevitLinkType>()
                .Select(t => Path.GetFileNameWithoutExtension(t.Name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public bool HasInstances(Document doc, string linkName)
            => GetLinkInstances(doc, linkName).Any();

        public int GetInstanceCount(Document doc, string linkName)
            => GetLinkInstances(doc, linkName).Count;

        public string GetCurrentWorksetName(Document doc, string linkName)
        {
            var instances = GetLinkInstances(doc, linkName);
            if (!instances.Any()) return "No instances";

            var worksetNames = new HashSet<string>();
            foreach (var instance in instances)
            {
                var param = instance.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                if (param != null && param.HasValue)
                {
                    int wsId = param.AsInteger();
                    Workset workset = new FilteredWorksetCollector(doc)
                        .FirstOrDefault(ws => ws.Id.IntegerValue == wsId);
                    if (workset != null)
                        worksetNames.Add(workset.Name);
                }
            }

            if (worksetNames.Count == 0) return "None";
            if (worksetNames.Count == 1) return worksetNames.First();
            return "MIXED";
        }

        public string CheckExistingWorkset(Document doc, string worksetName)
        {
            Workset existing = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset)
                .FirstOrDefault(ws =>
                    ws.Name.Equals(worksetName, StringComparison.OrdinalIgnoreCase));
            return existing?.Name;
        }

        public bool IsFullyAssigned(Document doc, string linkName, string proposedWorksetName)
        {
            var instances = GetLinkInstances(doc, linkName);
            if (!instances.Any()) return false;

            foreach (var instance in instances)
            {
                var param = instance.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                if (param == null || !param.HasValue) return false;

                int wsId = param.AsInteger();
                Workset ws = new FilteredWorksetCollector(doc)
                    .FirstOrDefault(w => w.Id.IntegerValue == wsId);

                if (ws == null || !ws.Name.Equals(proposedWorksetName, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }

        // ─── Name helpers ─────────────────────────────────────────────────────

        public void ResolveDuplicates(IEnumerable<WorksetItem> items)
        {
            // Seed with Grid 1 names so they are never collided into
            var usedNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items.Where(i => i.GridCategory == WorksetGridCategory.AlreadyAssigned))
                usedNames[item.ProposedWorksetName] = 1;

            foreach (var item in items.Where(i => i.GridCategory != WorksetGridCategory.AlreadyAssigned))
            {
                string baseName  = item.ProposedWorksetName;
                string finalName = baseName;
                int    count     = 1;

                while (usedNames.ContainsKey(finalName))
                {
                    count++;
                    finalName = $"{baseName}_{count}";
                }

                if (finalName != baseName)
                {
                    _log(new LogEntry(LogLevel.Warning,
                        $"Duplicate name '{baseName}' → renamed to '{finalName}'"));
                    item.ProposedWorksetName = finalName;
                }

                usedNames[finalName] = count;
            }
        }

        public string CleanName(string input)
        {
            if (string.IsNullOrEmpty(input)) return "Workset";

            string cleaned = input;

            char[] invalidChars = { ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~', '\t', '\n', '\r' };
            foreach (char c in invalidChars)
                cleaned = cleaned.Replace(c, '-');

            while (cleaned.Contains("--"))
                cleaned = cleaned.Replace("--", "-");

            cleaned = cleaned.Trim().Trim('-');

            if (string.IsNullOrEmpty(cleaned))
                cleaned = "Workset";

            if (cleaned.Length > 150)
            {
                string truncated = cleaned[..150];
                _log(new LogEntry(LogLevel.Warning,
                    $"Truncated '{cleaned}' to 150 chars: '{truncated}'"));
                cleaned = truncated;
            }

            return cleaned;
        }

        /// <summary>
        /// Variant used during live pattern editing — no log spam on every keystroke.
        /// </summary>
        public string CleanNameSilent(string input)
        {
            if (string.IsNullOrEmpty(input)) return "Workset";
            string cleaned = input;
            char[] invalidChars = { ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~', '\t', '\n', '\r' };
            foreach (char c in invalidChars)
                cleaned = cleaned.Replace(c, '-');
            while (cleaned.Contains("--"))
                cleaned = cleaned.Replace("--", "-");
            cleaned = cleaned.Trim().Trim('-');
            if (string.IsNullOrEmpty(cleaned)) cleaned = "Workset";
            if (cleaned.Length > 150) cleaned = cleaned[..150];
            return cleaned;
        }

        // ─── Write operations ─────────────────────────────────────────────────

        /// <summary>
        /// Creates (or re-uses) one workset per item and assigns that link's
        /// instances to it, one Transaction per link. Every link lands in
        /// <paramref name="summary"/> as a <see cref="LinkOutcome"/> — including
        /// failures, which are rolled back and do not stop the run.
        /// </summary>
        /// <param name="summary">
        /// Pre-filled with Mode / Label / link counts (and any outcome the caller
        /// already decided, e.g. a Resync skip). The same instance is returned.
        /// </param>
        /// <param name="onProgress">Called after each link with (processed, total).</param>
        public RunSummary CreateAndAssign(
            Document doc,
            IEnumerable<(string ProposedName, string LinkName, bool CreateNew)> items,
            RunSummary summary,
            Action<int, int> onProgress = null)
        {
            summary ??= new RunSummary();

            var queue = items?.ToList() ?? new List<(string, string, bool)>();
            int total = queue.Count;
            int processed = 0;
            int committed = 0;

            if (total == 0)
            {
                summary.FinishedAt = DateTime.Now;
                return summary;
            }

            // One undo step for the whole run (ARCHITECTURE.md §5): each link keeps its
            // own Transaction inside the group, so a failing link still rolls back on its
            // own, but the user undoes a 7-link run with one Ctrl+Z instead of seven.
            // Assimilate only when something committed — RollBack() on the group would
            // undo the links that already succeeded.
            using TransactionGroup group = new(doc, $"{summary.Mode}: {summary.Label}");
            group.Start();

            foreach (var (proposedName, linkName, createNew) in queue)
            {
                processed++;

                var outcome = new LinkOutcome
                {
                    LinkName    = linkName,
                    WorksetName = proposedName
                };

                using Transaction tx = new(doc,
                    createNew ? $"Create workset: {proposedName}" : $"Resync workset: {proposedName}");
                try
                {
                    tx.Start();

                    WorksetId targetId;
                    bool createdNew = false;

                    if (createNew)
                    {
                        Workset workset = Workset.Create(doc, proposedName);
                        targetId   = workset.Id;
                        createdNew = true;
                        _log(new LogEntry(LogLevel.Info, $"Created workset: '{proposedName}'"));
                    }
                    else
                    {
                        Workset existing = new FilteredWorksetCollector(doc)
                            .OfKind(WorksetKind.UserWorkset)
                            .FirstOrDefault(ws =>
                                ws.Name.Equals(proposedName, StringComparison.OrdinalIgnoreCase));

                        if (existing == null)
                        {
                            _log(new LogEntry(LogLevel.Error,
                                $"Workset '{proposedName}' not found — skipped."));
                            tx.RollBack();

                            outcome.Outcome = RunOutcome.Skipped;
                            outcome.Note    = "Workset does not exist — use Create";
                            summary.Outcomes.Add(outcome);
                            onProgress?.Invoke(processed, total);
                            continue;
                        }
                        targetId = existing.Id;
                    }

                    AssignResult result = AssignLinkInstances(doc, linkName, targetId);

                    outcome.InstancesAssigned = result.Assigned;
                    outcome.InstancesTotal    = result.Total;
                    outcome.Note              = result.Note;

                    if (result.Total == 0)
                    {
                        // Grid 3 — nothing placed in the model yet.
                        outcome.Outcome = createdNew ? RunOutcome.CreatedNoInstances : RunOutcome.Skipped;
                        outcome.Note    = "No placed instances — assignment happens when the link is loaded";
                        _log(new LogEntry(LogLevel.Warning,
                            $"'{linkName}' has no placed instances — workset '{proposedName}' left empty."));
                    }
                    else if (result.Assigned == 0)
                    {
                        outcome.Outcome = RunOutcome.Missed;
                        _log(new LogEntry(LogLevel.Warning,
                            $"Missed '{linkName}' — 0 of {result.Total} instance(s) could be assigned to '{proposedName}'."));
                    }
                    else
                    {
                        outcome.Outcome = createdNew ? RunOutcome.Created : RunOutcome.Reused;
                        _log(new LogEntry(LogLevel.Info,
                            $"Assigned {result.Assigned} instance(s) of '{linkName}' → '{proposedName}'"));
                        if (!string.IsNullOrWhiteSpace(result.Note))
                            _log(new LogEntry(LogLevel.Warning, $"'{linkName}': {result.Note}"));
                    }

                    tx.Commit();
                    committed++;

                    // Only after the commit: a workset created in a transaction that
                    // then threw does not exist, so it must not be counted.
                    outcome.WorksetWasCreated = createdNew;
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    _log(new LogEntry(LogLevel.Error, $"Failed on '{proposedName}': {ex.Message}"));

                    outcome.Outcome = RunOutcome.Error;
                    outcome.Note    = ex.Message;
                }

                // The only path that adds its outcome early is the
                // "workset not found" skip above, which also `continue`s.
                summary.Outcomes.Add(outcome);

                onProgress?.Invoke(processed, total);
            }

            if (committed > 0) group.Assimilate();
            else                group.RollBack();

            summary.FinishedAt = DateTime.Now;
            return summary;
        }

        // ─── Private helpers ──────────────────────────────────────────────────

        /// <summary>Instances written, instances found, and a note about the rest.</summary>
        private readonly record struct AssignResult(int Assigned, int Total, string Note);

        private AssignResult AssignLinkInstances(Document doc, string linkName, WorksetId worksetId)
        {
            var instances = GetLinkInstances(doc, linkName);
            int assigned = 0;
            int blocked  = 0;

            foreach (RevitLinkInstance instance in instances)
            {
                Parameter param = instance.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                if (param == null || param.IsReadOnly)
                {
                    blocked++;
                    continue;
                }

                try
                {
                    if (param.Set(worksetId.IntegerValue)) assigned++;
                    else blocked++;
                }
                catch (Exception)
                {
                    // Pinned / borrowed by another user / otherwise refused —
                    // counted, never fatal to the rest of the run.
                    blocked++;
                }
            }

            string note = blocked == 0
                ? string.Empty
                : $"{blocked} instance(s) not assigned (workset parameter read-only, pinned, or owned by another user)";

            return new AssignResult(assigned, instances.Count, note);
        }

        private List<RevitLinkInstance> GetLinkInstances(Document doc, string linkName)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Where(i =>
                {
                    var linkDoc = i.GetLinkDocument();
                    if (linkDoc == null) return false;
                    string title = Path.GetFileNameWithoutExtension(linkDoc.Title);
                    return title.Equals(linkName, StringComparison.OrdinalIgnoreCase);
                })
                .ToList();
        }
    }
}
