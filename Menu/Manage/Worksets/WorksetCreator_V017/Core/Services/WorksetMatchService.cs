// ==============================================
// File: WorksetMatchService.cs
// Layer: Core/Services  (no Revit API, no UI — pure functions over plain data)
// Workset Creator V017 — this file was introduced in V016.
//
// Finds the workset a link already has, even when it was created under a
// different naming pattern — without being told what the patterns are.
//
// THE IDEA
// Do not strip the pattern off the workset name. Search for the LINK name
// inside the workset name; whatever surrounds it IS the pattern. The link name
// is known exactly, so it is the anchor, and the residue falls out:
//
//   link     WAMI-ST-Z1-Podium          -> WAMISTZ1PODIUM
//   workset  Z-LINK-WAMI ST Z1 Podium   -> ZLINK[WAMISTZ1PODIUM]      residue "Z-LINK-" + ""
//   workset  +Link(WAMI-ST-Z1-Podium)   -> LINK[WAMISTZ1PODIUM]       residue "+Link("  + ")"
//   workset  WS_WAMI-ST-Z1-Podium_RVT   -> WS[WAMISTZ1PODIUM]RVT      residue "WS_"     + "_RVT"
//
// Then count each residue across the whole model. A pair shared by many
// worksets is the project's convention; one used once is a coincidence. That
// count — not a hard-coded list — decides whether the tool pre-selects Rename
// or sends the row to the user for review.
//
// Three guards stop the obvious false positive, a short link name sitting
// inside a longer one (WAMI-AR-Z1 inside "+Link(WAMI-AR-Z1-Tower)"):
//   1. A link name shorter than MinNormalisedNameLength is matched by exact
//      name only — no containment test at all.
//   2. The residue may be at most MaxResiduePercent of the workset name, so a
//      short name inside a long unrelated workset does not qualify.
//   3. A workset can be claimed by one link only, and the longest link name
//      wins. The loser reports no match and gets a fresh workset.
//
// Nothing in here mutates a document or touches the UI, so the whole decision
// is testable on plain strings.
// ==============================================

using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.WorksetCreator.V017.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revit26_Plugin.WorksetCreator.V017.Core.Services
{
    public sealed class WorksetMatchService
    {
        /// <summary>
        /// Largest share of a workset name the residue may occupy. At 60%, the link
        /// name must account for at least 40% of the workset name to be a candidate.
        /// </summary>
        private const int MaxResiduePercent = 60;

        private readonly Action<LogEntry> _log;

        public WorksetMatchService(Action<LogEntry> logger) => _log = logger;

        /// <summary>One link's identity as the matcher needs it.</summary>
        public sealed class MatchInput
        {
            public int    LinkTypeId   { get; set; }

            /// <summary>The link's own name — the anchor searched for.</summary>
            public string LinkName     { get; set; } = string.Empty;

            /// <summary>The pattern-assembled name, for the exact-match test.</summary>
            public string ProposedName { get; set; } = string.Empty;
        }

        /// <summary>
        /// Matches every link against every existing workset and returns the verdicts
        /// plus the conventions discovered on the way. Pure — same inputs, same result.
        /// </summary>
        public MatchResult Match(
            IEnumerable<MatchInput> links,
            IEnumerable<WorksetInfo> worksets,
            WorksetCreatorSettings settings)
        {
            var result = new MatchResult();

            List<MatchInput>   linkList    = links?.ToList()    ?? new List<MatchInput>();
            List<WorksetInfo>  worksetList = worksets?.ToList() ?? new List<WorksetInfo>();
            settings ??= new WorksetCreatorSettings();

            result.WorksetsConsidered = worksetList.Count;

            foreach (MatchInput link in linkList)
                result.ByLinkTypeId[link.LinkTypeId] = new LinkMatch { LinkTypeId = link.LinkTypeId };

            if (linkList.Count == 0 || worksetList.Count == 0)
            {
                FinaliseAll(result, settings);
                return result;
            }

            // ── Pass 1 — normalise every workset once, keeping an index map back
            //            to the original text so the residue reads as written.
            var normalisedWorksets = worksetList
                .Where(w => w != null && !string.IsNullOrWhiteSpace(w.Name))
                .Select(w => new { Workset = w, Norm = Normalise(w.Name) })
                .Where(x => x.Norm.Value.Length > 0)
                .ToList();

            // ── Pass 2 — collect raw candidates: exact name, then containment.
            var raw = new List<WorksetCandidate>();
            var rawByLink = new Dictionary<int, List<WorksetCandidate>>();

            foreach (MatchInput link in linkList)
            {
                var forThisLink = new List<WorksetCandidate>();
                rawByLink[link.LinkTypeId] = forThisLink;

                NormalisedText linkNorm = Normalise(link.LinkName);

                foreach (var candidateWorkset in normalisedWorksets)
                {
                    WorksetInfo ws = candidateWorkset.Workset;

                    // Exact match on the proposed name — the V015/V016 behaviour,
                    // kept as the strongest signal and exempt from every guard.
                    if (!string.IsNullOrWhiteSpace(link.ProposedName) &&
                        ws.Name.Equals(link.ProposedName, StringComparison.OrdinalIgnoreCase))
                    {
                        forThisLink.Add(new WorksetCandidate
                        {
                            Workset            = ws,
                            Prefix             = string.Empty,
                            Suffix             = string.Empty,
                            IsExact            = true,
                            Score              = 100,
                            ClaimantNameLength = linkNorm.Value.Length
                        });
                        continue;
                    }

                    // Guard 1 — too short to fuzzy-match safely.
                    if (linkNorm.Value.Length < settings.MinNormalisedNameLength)
                        continue;

                    int at = candidateWorkset.Norm.Value.IndexOf(linkNorm.Value, StringComparison.Ordinal);
                    if (at < 0) continue;

                    int worksetLen = candidateWorkset.Norm.Value.Length;
                    int residueLen = worksetLen - linkNorm.Value.Length;

                    // Guard 2 — the wrapper may not dominate the name.
                    if (residueLen * 100 > MaxResiduePercent * worksetLen)
                        continue;

                    (string prefix, string suffix) =
                        SplitResidue(ws.Name, candidateWorkset.Norm, at, linkNorm.Value.Length);

                    var candidate = new WorksetCandidate
                    {
                        Workset            = ws,
                        Prefix             = prefix,
                        Suffix             = suffix,
                        IsExact            = false,
                        Score              = worksetLen == 0 ? 0 : linkNorm.Value.Length * 100 / worksetLen,
                        ClaimantNameLength = linkNorm.Value.Length
                    };

                    forThisLink.Add(candidate);
                    raw.Add(candidate);
                }
            }

            // ── Pass 3 — learn the conventions by counting residues across the
            //            model. Counted per distinct workset, so one workset
            //            claimed by two links does not inflate its own residue.
            Dictionary<string, int> residueCounts = CountResidues(raw);
            foreach (WorksetCandidate candidate in raw)
                candidate.ResidueOccurrences = residueCounts.TryGetValue(ResidueKey(candidate), out int n) ? n : 0;

            BuildConventionList(result, residueCounts, raw, settings);

            // ── Pass 4 — exclusivity: one workset, one link, longest name wins.
            ApplyExclusivity(rawByLink);

            // ── Pass 5 — pick the best surviving candidate per link and grade it.
            foreach (MatchInput link in linkList)
            {
                LinkMatch match = result.ByLinkTypeId[link.LinkTypeId];
                List<WorksetCandidate> surviving = rawByLink[link.LinkTypeId];

                foreach (WorksetCandidate candidate in Rank(surviving))
                    match.Candidates.Add(candidate);
            }

            FinaliseAll(result, settings);
            LogSummary(result, settings);

            return result;
        }

        // ─── Normalising ──────────────────────────────────────────────────────

        /// <summary>
        /// A normalised string plus, for each of its characters, the index it came
        /// from in the original. The map is what lets the residue be reported as the
        /// user wrote it ("+Link(") rather than as the matcher saw it ("LINK").
        /// </summary>
        public readonly struct NormalisedText
        {
            public string Value { get; }
            public int[]  SourceIndex { get; }

            public NormalisedText(string value, int[] sourceIndex)
            {
                Value       = value;
                SourceIndex = sourceIndex;
            }
        }

        /// <summary>
        /// Uppercase, then drop everything that is not a letter or a digit. Case,
        /// spaces, brackets, underscores and dashes all stop mattering, which is
        /// exactly what makes "+Link(X)" and "Z-LINK-X" comparable.
        /// </summary>
        public static NormalisedText Normalise(string input)
        {
            if (string.IsNullOrEmpty(input))
                return new NormalisedText(string.Empty, Array.Empty<int>());

            var sb  = new StringBuilder(input.Length);
            var map = new List<int>(input.Length);

            for (int i = 0; i < input.Length; i++)
            {
                char c = input[i];
                if (!char.IsLetterOrDigit(c)) continue;
                sb.Append(char.ToUpperInvariant(c));
                map.Add(i);
            }

            return new NormalisedText(sb.ToString(), map.ToArray());
        }

        /// <summary>Convenience overload for callers that only want the text.</summary>
        public static string NormaliseText(string input) => Normalise(input).Value;

        /// <summary>
        /// Cuts the original workset name either side of the matched run, using the
        /// index map so the pieces are the real characters the user typed.
        /// </summary>
        private static (string Prefix, string Suffix) SplitResidue(
            string originalWorksetName, NormalisedText worksetNorm, int matchStart, int matchLength)
        {
            if (string.IsNullOrEmpty(originalWorksetName) || matchLength <= 0)
                return (string.Empty, string.Empty);

            int firstSourceIndex = worksetNorm.SourceIndex[matchStart];
            int lastSourceIndex  = worksetNorm.SourceIndex[matchStart + matchLength - 1];

            string prefix = originalWorksetName[..firstSourceIndex];
            string suffix = lastSourceIndex + 1 < originalWorksetName.Length
                ? originalWorksetName[(lastSourceIndex + 1)..]
                : string.Empty;

            return (prefix, suffix);
        }

        // ─── Residues and conventions ─────────────────────────────────────────

        /// <summary>
        /// Key a residue by its normalised prefix/suffix, so "+Link(" and "+LINK("
        /// are the same convention. \u0001 separates the halves — it cannot occur
        /// in a Revit workset name.
        /// </summary>
        private static string ResidueKey(WorksetCandidate candidate) =>
            ResidueKey(candidate.Prefix, candidate.Suffix);

        private static string ResidueKey(string prefix, string suffix) =>
            $"{NormaliseText(prefix)}\u0001{NormaliseText(suffix)}";

        /// <summary>
        /// How many distinct worksets exhibit each residue. Deduped on workset id:
        /// the same workset matching two links is still one sighting of its pattern.
        /// </summary>
        private static Dictionary<string, int> CountResidues(List<WorksetCandidate> raw)
        {
            var seen   = new Dictionary<string, HashSet<int>>();

            foreach (WorksetCandidate candidate in raw)
            {
                string key = ResidueKey(candidate);
                if (!seen.TryGetValue(key, out HashSet<int> ids))
                    seen[key] = ids = new HashSet<int>();
                ids.Add(candidate.Workset?.Id ?? 0);
            }

            return seen.ToDictionary(kv => kv.Key, kv => kv.Value.Count);
        }

        private static void BuildConventionList(
            MatchResult result,
            Dictionary<string, int> residueCounts,
            List<WorksetCandidate> raw,
            WorksetCreatorSettings settings)
        {
            // Carry one readable spelling per key — the first one encountered.
            var display = new Dictionary<string, WorksetCandidate>();
            foreach (WorksetCandidate candidate in raw)
            {
                string key = ResidueKey(candidate);
                if (!display.ContainsKey(key)) display[key] = candidate;
            }

            foreach (KeyValuePair<string, int> entry in residueCounts.OrderByDescending(e => e.Value))
            {
                if (!display.TryGetValue(entry.Key, out WorksetCandidate sample)) continue;

                result.Conventions.Add(new NamingConvention
                {
                    Prefix      = sample.Prefix,
                    Suffix      = sample.Suffix,
                    Occurrences = entry.Value,
                    IsAccepted  = entry.Value >= settings.ConventionMinOccurrences
                });
            }
        }

        // ─── Exclusivity and ranking ──────────────────────────────────────────

        /// <summary>
        /// Guard 3. A workset belongs to at most one link; where several claim it,
        /// the longest normalised link name keeps it, because it is the more
        /// specific reading. Ties are broken by score, then by the lower link id so
        /// the outcome is deterministic rather than dictionary-order.
        /// </summary>
        private static void ApplyExclusivity(Dictionary<int, List<WorksetCandidate>> rawByLink)
        {
            var winnerByWorkset = new Dictionary<int, (int LinkTypeId, WorksetCandidate Candidate)>();

            foreach (KeyValuePair<int, List<WorksetCandidate>> entry in rawByLink.OrderBy(e => e.Key))
            {
                foreach (WorksetCandidate candidate in entry.Value)
                {
                    int worksetId = candidate.Workset?.Id ?? 0;

                    if (!winnerByWorkset.TryGetValue(worksetId, out var held))
                    {
                        winnerByWorkset[worksetId] = (entry.Key, candidate);
                        continue;
                    }

                    if (Beats(candidate, held.Candidate))
                        winnerByWorkset[worksetId] = (entry.Key, candidate);
                }
            }

            // Drop every candidate that did not win its workset.
            foreach (KeyValuePair<int, List<WorksetCandidate>> entry in rawByLink)
            {
                entry.Value.RemoveAll(candidate =>
                {
                    int worksetId = candidate.Workset?.Id ?? 0;
                    return !winnerByWorkset.TryGetValue(worksetId, out var held) ||
                           held.LinkTypeId != entry.Key ||
                           !ReferenceEquals(held.Candidate, candidate);
                });
            }
        }

        private static bool Beats(WorksetCandidate challenger, WorksetCandidate holder)
        {
            if (challenger.IsExact != holder.IsExact) return challenger.IsExact;
            if (challenger.ClaimantNameLength != holder.ClaimantNameLength)
                return challenger.ClaimantNameLength > holder.ClaimantNameLength;
            return challenger.Score > holder.Score;
        }

        /// <summary>Exact first, then the most-used convention, then the tightest wrapper.</summary>
        private static IEnumerable<WorksetCandidate> Rank(IEnumerable<WorksetCandidate> candidates) =>
            candidates
                .OrderByDescending(c => c.IsExact)
                .ThenByDescending(c => c.ResidueOccurrences)
                .ThenByDescending(c => c.Score)
                .ThenBy(c => c.Workset?.Name, StringComparer.OrdinalIgnoreCase);

        // ─── Grading ──────────────────────────────────────────────────────────

        private static void FinaliseAll(MatchResult result, WorksetCreatorSettings settings)
        {
            foreach (LinkMatch match in result.ByLinkTypeId.Values)
                Grade(match, settings);
        }

        /// <summary>
        /// Turns a link's candidate list into a confidence and the action the UI
        /// pre-selects. The rules are the ones agreed with Rafi: one workset per
        /// link always, rename on a confident match, user decides otherwise.
        /// </summary>
        private static void Grade(LinkMatch match, WorksetCreatorSettings settings)
        {
            if (match.Candidates.Count == 0)
            {
                match.Confidence      = MatchConfidence.None;
                match.Best            = null;
                match.SuggestedAction = WorksetAction.Create;
                match.Explanation     = "No existing workset resembles this link — a new one will be created.";
                return;
            }

            match.Best = match.Candidates[0];

            if (match.Candidates.Count > 1)
            {
                match.Confidence      = MatchConfidence.Ambiguous;
                match.SuggestedAction = WorksetAction.Rename;
                match.Explanation =
                    $"{match.Candidates.Count} worksets already look like this link's " +
                    $"({string.Join(" | ", match.Candidates.Select(c => c.Workset?.Name))}). " +
                    "Pick the one to keep — the others are emptied into it.";
                return;
            }

            WorksetCandidate best = match.Best;

            if (best.IsExact)
            {
                match.Confidence      = MatchConfidence.Exact;
                match.SuggestedAction = WorksetAction.Reuse;
                match.Explanation =
                    $"Workset '{best.Workset?.Name}' already carries the proposed name — " +
                    "instances are assigned into it and nothing is created.";
                return;
            }

            if (best.ResidueOccurrences >= settings.ConventionMinOccurrences)
            {
                match.Confidence      = MatchConfidence.Convention;
                match.SuggestedAction = WorksetAction.Rename;
                match.Explanation =
                    $"'{best.Workset?.Name}' matches through the pattern \"{best.ResidueDisplay}\", " +
                    $"used by {best.ResidueOccurrences} workset(s) in this model — a project convention. " +
                    "Renaming it keeps one workset for this link.";
                return;
            }

            match.Confidence      = MatchConfidence.Weak;
            match.SuggestedAction = WorksetAction.Rename;
            match.Explanation =
                $"'{best.Workset?.Name}' matches through the pattern \"{best.ResidueDisplay}\", " +
                $"but only {best.ResidueOccurrences} workset(s) use it — too few to be sure it is a " +
                "convention rather than a coincidence. Please confirm.";
        }

        // ─── Logging ──────────────────────────────────────────────────────────

        private void LogSummary(MatchResult result, WorksetCreatorSettings settings)
        {
            if (_log == null) return;

            var accepted = result.Conventions.Where(c => c.IsAccepted).ToList();
            var rejected = result.Conventions.Where(c => !c.IsAccepted).ToList();

            Log(LogLevel.Info,
                $"Matched {result.ByLinkTypeId.Count} link(s) against {result.WorksetsConsidered} workset(s).");

            if (accepted.Count > 0)
                Log(LogLevel.Info,
                    "Conventions accepted (" + settings.ConventionMinOccurrences + "+ sightings): " +
                    string.Join(", ", accepted.Select(c => $"\"{c.Display}\" x{c.Occurrences}")));

            if (rejected.Count > 0)
                Log(LogLevel.Warning,
                    "Patterns below the threshold, routed to review: " +
                    string.Join(", ", rejected.Select(c => $"\"{c.Display}\" x{c.Occurrences}")));

            int exact      = result.ByLinkTypeId.Values.Count(m => m.Confidence == MatchConfidence.Exact);
            int convention = result.ByLinkTypeId.Values.Count(m => m.Confidence == MatchConfidence.Convention);
            int weak       = result.ByLinkTypeId.Values.Count(m => m.Confidence == MatchConfidence.Weak);
            int ambiguous  = result.ByLinkTypeId.Values.Count(m => m.Confidence == MatchConfidence.Ambiguous);
            int none       = result.ByLinkTypeId.Values.Count(m => m.Confidence == MatchConfidence.None);

            Log(LogLevel.Info,
                $"Match verdicts: {none} no match, {exact} exact, {convention} convention, " +
                $"{weak} weak, {ambiguous} ambiguous.");

            foreach (LinkMatch match in result.ByLinkTypeId.Values.Where(m => m.NeedsReview))
                Log(LogLevel.Warning, $"Needs review — {match.Explanation}");
        }

        private void Log(LogLevel level, string message) => _log?.Invoke(new LogEntry(level, message));
    }
}
