// ==============================================
// File: LinkLoadState.cs
// Layer: Core/Models  (no Revit API — CLAUDE.md)
// Workset Creator V017 — this file was introduced in V016.
//
// V015 had no concept of load state: it discovered a link's instances through
// RevitLinkInstance.GetLinkDocument(), which returns null for an unloaded
// link, so "unloaded but placed" and "never placed" were indistinguishable and
// both landed in Grid 3 with an empty workset. V016 counts instances by link
// type id instead, which works whatever the load state, and carries the state
// itself here so the UI can say which it is.
// ==============================================

namespace Revit26_Plugin_Revised.WorksetCreator.V017.Core.Models
{
    /// <summary>
    /// Mirror of Revit's <c>LinkedFileStatus</c>, flattened to the three cases
    /// this tool treats differently. Mapped in <c>WorksetService.ScanLinks</c>
    /// so nothing outside Core/Services touches the Revit enum.
    /// </summary>
    public enum LinkLoadState
    {
        /// <summary>Link is loaded — geometry is in the model.</summary>
        Loaded,

        /// <summary>
        /// Link is unloaded (or locally unloaded). Its RevitLinkInstance elements
        /// still exist in the host and still carry a workset, so they can still be
        /// counted — and, Revit permitting, re-assigned.
        /// </summary>
        Unloaded,

        /// <summary>Revit cannot resolve the file (moved / renamed / deleted).</summary>
        NotFound
    }
}
