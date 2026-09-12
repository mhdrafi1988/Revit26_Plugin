// =======================================================
// File: MultiSlopeRunSummary.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.MultiCopies28
// Purpose: Aggregate result returned once by MultiSlopeVariantEngine
//          via MultiSlopePayload.OnCompleted, after every active
//          slope row has been processed (or the run aborted early
//          on a guard failure, e.g. roof not found / not workshared).
// =======================================================

using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPoint.MultiCopies28.Core.Models
{
    public class MultiSlopeRunSummary
    {
        /// <summary>False only when the run aborted before any slope could be attempted (roof not found, not workshared, no active slopes selected).</summary>
        public bool Success { get; set; }

        /// <summary>Set when Success is false.</summary>
        public string ErrorMessage { get; set; }

        public List<MultiSlopeVariantResult> Results { get; set; } = new List<MultiSlopeVariantResult>();

        public int CopiesCreated { get; set; }
        public int WorksetsCreated { get; set; }
        public int Failed { get; set; }
    }
}
