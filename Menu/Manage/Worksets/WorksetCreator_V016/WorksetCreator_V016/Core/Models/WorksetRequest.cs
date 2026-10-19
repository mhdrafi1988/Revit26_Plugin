// ==============================================
// File: WorksetRequest.cs
// Layer: Core/Models  (no Revit API — CLAUDE.md)
// NEW in Workset Creator V016.
//
// One unit of work handed to WorksetService.CreateAndAssign. V015 passed a
// (ProposedName, LinkName, CreateNew) tuple, which could only find the link's
// instances by name — and name matching went through GetLinkDocument(), which
// is null for an unloaded link. The request now carries the link type id, so
// the service finds instances by type, and the unloaded flag, so a refusal can
// be reported with the reason instead of a bare "0 assigned".
//
// Added later in V016 (duplicate prevention):
//   NEW  Action — Create / Reuse / Rename / Adopt / Skip. The decision is made
//        once, in the ViewModel, from the matcher's verdict and whatever the
//        user overrode; the service carries it out rather than re-deciding.
//        CreateNew is kept and derived from Action so nothing that reads it
//        has to change.
//   NEW  MatchedWorksetId / RenameFrom — the workset a Rename or Adopt acts on,
//        identified by id rather than by name, because the name is the very
//        thing a rename changes.
// ==============================================

namespace Revit26_Plugin.WorksetCreator.V016.Core.Models
{
    public sealed class WorksetRequest
    {
        /// <summary>ElementId.IntegerValue of the RevitLinkType to act on.</summary>
        public int LinkTypeId { get; set; }

        /// <summary>Display name — for the log, the report and nothing else.</summary>
        public string LinkName { get; set; } = string.Empty;

        /// <summary>The workset this link must end up in once the request is done.</summary>
        public string ProposedName { get; set; } = string.Empty;

        /// <summary>What to do. Set by the ViewModel; the service does not re-decide.</summary>
        public WorksetAction Action { get; set; } = WorksetAction.Create;

        /// <summary>
        /// True for Create on a name that does not exist yet. The service still
        /// re-checks before creating — the pattern box can be edited after the
        /// scan, so this flag can be stale by the time the run starts.
        /// </summary>
        public bool CreateNew { get; set; }

        /// <summary>
        /// WorksetId.IntegerValue of the existing workset a Rename or Adopt acts on.
        /// 0 for Create and for Reuse, which both find their target by name.
        /// </summary>
        public int MatchedWorksetId { get; set; }

        /// <summary>
        /// The matched workset's name before a Rename, kept for the log and the report.
        /// Identity travels as <see cref="MatchedWorksetId"/>, never as this string —
        /// the name is what the rename changes.
        /// </summary>
        public string RenameFrom { get; set; } = string.Empty;

        /// <summary>Used only to explain a refused assignment in the run report.</summary>
        public bool IsUnloaded { get; set; }

        /// <summary>True when this request renames an existing workset before assigning.</summary>
        public bool IsRename => Action == WorksetAction.Rename && MatchedWorksetId != 0;

        /// <summary>True when this request assigns into the matched workset as it stands.</summary>
        public bool IsAdopt => Action == WorksetAction.Adopt && MatchedWorksetId != 0;
    }
}
