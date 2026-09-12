// =======================================================
// File: MultiSlopeVariantResult.cs
// Namespace: Revit26_Plugin.AutoSlopeByPoint.MultiCopies28
// Purpose: Outcome of processing a single active slope row —
//          one roof copy, one SubTransaction. Collected into
//          MultiSlopeRunSummary.Results by MultiSlopeVariantEngine.
// =======================================================

using Autodesk.Revit.DB;

namespace Revit26_Plugin.AutoSlopeByPoint.MultiCopies28.Core.Models
{
    public class MultiSlopeVariantResult
    {
        public int RowIndex { get; set; }
        public double Percent { get; set; }

        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        /// <summary>Id of the roof copy created for this slope. Null if the copy itself failed.</summary>
        public ElementId CopyRoofId { get; set; }

        public string WorksetName { get; set; }
        public bool WorksetWasCreated { get; set; }

        /// <summary>Full engine result for this copy (vertices processed, elevations, export path, etc.). Null if the run failed before the engine produced a result.</summary>
        public AutoSlopeResult EngineResult { get; set; }
    }
}
