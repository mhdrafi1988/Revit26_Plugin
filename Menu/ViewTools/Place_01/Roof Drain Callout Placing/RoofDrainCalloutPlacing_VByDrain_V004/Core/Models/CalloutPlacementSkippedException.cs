using System;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.VByDrain.V004.Models
{
    /// <summary>
    /// Thrown to signal an expected, non-critical skip during callout placement
    /// (e.g. opening too close to another already-placed callout this run,
    /// degenerate opening geometry). Caught per-item and logged as Warning — never
    /// surfaced as a dialog.
    /// </summary>
    public class CalloutPlacementSkippedException : Exception
    {
        public CalloutPlacementSkippedException(string message) : base(message) { }
    }
}
