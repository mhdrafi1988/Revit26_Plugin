// ==============================================
// File: WorksetMatchModels.cs
// Layer: Core/Models  (no Revit API — CLAUDE.md)
// NEW in Workset Creator V016.
//
// Plain data for the matching layer (Core/Services/WorksetMatchService.cs).
// Nothing here knows about Revit or WPF: a workset is a name, an int id and an
// element count, which is everything the matcher needs and nothing it can
// misuse.
//
// Why this layer exists at all: V016 as shipped matched an existing workset by
// exact, case-insensitive name only. A workset created under a different
// naming pattern was invisible to it, so the tool created a second workset for
// the same link and left the first one holding instances. One link, two
// worksets. These types carry the evidence for the decision that prevents it.
// ==============================================

using System.Collections.Generic;

namespace Revit26_Plugin.WorksetCreator.V016.Core.Models
{
    /// <summary>How much the matcher trusts a link's best candidate.</summary>
    public enum MatchConfidence
    {
        /// <summary>Nothing in the model looks like this link's workset.</summary>
        None,

        /// <summary>A workset already carries the proposed name, character for character.</summary>
        Exact,

        /// <summary>
        /// Matched through a prefix/suffix residue that enough other worksets share
        /// for it to be a project convention. Rename is pre-selected.
        /// </summary>
        Convention,

        /// <summary>
        /// Matched, but through a residue only one or two worksets use — a
        /// coincidence as easily as a convention. Routed to Needs Review.
        /// </summary>
        Weak,

        /// <summary>
        /// More than one workset survived as a candidate. The model already holds
        /// duplicates for this link; the user picks the survivor.
        /// </summary>
        Ambiguous
    }

    /// <summary>What the tool decided to do with one link, or what the user chose.</summary>
    public enum WorksetAction
    {
        /// <summary>Create the pattern-named workset and assign the link's instances.</summary>
        Create,

        /// <summary>A workset already has the pattern name — assign into it, create nothing.</summary>
        Reuse,

        /// <summary>Rename the matched workset to the pattern name, then assign.</summary>
        Rename,

        /// <summary>Keep the matched workset's existing name and assign into it.</summary>
        Adopt,

        /// <summary>Do nothing for this link. Reported, never silent.</summary>
        Skip
    }

    /// <summary>One user workset, flattened for the matcher.</summary>
    public sealed class WorksetInfo
    {
        /// <summary>WorksetId.IntegerValue — the identity.</summary>
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Elements currently sitting in this workset. Surfaced in the UI tooltip so
        /// a workset holding more than this link's instances is visible before a
        /// rename is accepted.
        /// </summary>
        public int ElementCount { get; set; }
    }

    /// <summary>
    /// One existing workset that could be this link's, with the evidence for it.
    /// The residue is what was left of the workset name once the link name was
    /// found inside it — in other words, the naming pattern, discovered rather
    /// than declared.
    /// </summary>
    public sealed class WorksetCandidate
    {
        public WorksetInfo Workset { get; set; }

        /// <summary>Text before the link name, as written in the workset name.</summary>
        public string Prefix { get; set; } = string.Empty;

        /// <summary>Text after the link name, as written in the workset name.</summary>
        public string Suffix { get; set; } = string.Empty;

        /// <summary>True when the workset name equals the proposed name outright.</summary>
        public bool IsExact { get; set; }

        /// <summary>How many worksets in this model share this prefix/suffix pair.</summary>
        public int ResidueOccurrences { get; set; }

        /// <summary>
        /// 0–100. 100 for an exact name match, otherwise the share of the workset
        /// name the link name accounts for — a tighter wrapper scores higher.
        /// </summary>
        public int Score { get; set; }

        /// <summary>Normalised length of the link name that claimed this workset.</summary>
        public int ClaimantNameLength { get; set; }

        /// <summary>"+Link(…)" / "Z-LINK-…" / "…_RVT" — for the log and the tooltip.</summary>
        public string ResidueDisplay => FormatResidue(Prefix, Suffix);

        internal static string FormatResidue(string prefix, string suffix)
        {
            bool hasPrefix = !string.IsNullOrEmpty(prefix);
            bool hasSuffix = !string.IsNullOrEmpty(suffix);

            if (!hasPrefix && !hasSuffix) return "exact name";
            if (hasPrefix && hasSuffix)   return $"{prefix}…{suffix}";
            return hasPrefix ? $"{prefix}…" : $"…{suffix}";
        }
    }

    /// <summary>The matcher's verdict for one link.</summary>
    public sealed class LinkMatch
    {
        public int LinkTypeId { get; set; }

        public MatchConfidence Confidence { get; set; } = MatchConfidence.None;

        /// <summary>Null when Confidence is None.</summary>
        public WorksetCandidate Best { get; set; }

        /// <summary>Every surviving candidate, best first. One entry unless Ambiguous.</summary>
        public List<WorksetCandidate> Candidates { get; } = new();

        /// <summary>The action the tool pre-selects for this link.</summary>
        public WorksetAction SuggestedAction { get; set; } = WorksetAction.Create;

        /// <summary>True when the user must decide before this link can run.</summary>
        public bool NeedsReview =>
            Confidence == MatchConfidence.Weak || Confidence == MatchConfidence.Ambiguous;

        /// <summary>One line explaining the verdict — goes in the log and the tooltip.</summary>
        public string Explanation { get; set; } = string.Empty;
    }

    /// <summary>
    /// A prefix/suffix pair the matcher found in this model, with how many
    /// worksets use it. Shown in the window so the user can see the logic
    /// agreeing with reality before pressing anything.
    /// </summary>
    public sealed class NamingConvention
    {
        public string Prefix { get; set; } = string.Empty;
        public string Suffix { get; set; } = string.Empty;
        public int    Occurrences { get; set; }

        /// <summary>True when Occurrences reached the settings threshold.</summary>
        public bool IsAccepted { get; set; }

        public string Display => WorksetCandidate.FormatResidue(Prefix, Suffix);

        public string Tooltip => IsAccepted
            ? $"Used by {Occurrences} workset(s) — treated as a project convention, " +
              "so a link matching it has Rename pre-selected."
            : $"Used by only {Occurrences} workset(s) — below the threshold, " +
              "so a link matching it goes to Needs Review instead.";
    }

    /// <summary>Everything one matching pass produced.</summary>
    public sealed class MatchResult
    {
        /// <summary>Keyed on LinkRecord.TypeId. Every scanned link has an entry.</summary>
        public Dictionary<int, LinkMatch> ByLinkTypeId { get; } = new();

        /// <summary>Residues found, most used first.</summary>
        public List<NamingConvention> Conventions { get; } = new();

        /// <summary>Worksets read for this pass — for the log line.</summary>
        public int WorksetsConsidered { get; set; }

        public LinkMatch For(int linkTypeId) =>
            ByLinkTypeId.TryGetValue(linkTypeId, out LinkMatch match)
                ? match
                : new LinkMatch { LinkTypeId = linkTypeId };
    }
}
