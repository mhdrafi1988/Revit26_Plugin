// =======================================================
// File: InnerLoopDividerResult.cs
// Location: Core/Models/
// New in V009. Plain result object returned by InnerLoopDividerEngine.
// No UI, no WPF, no ViewModel references.
// =======================================================

using System.Collections.Generic;

namespace Revit26_Plugin.InnerLoopDivider.V009.Core.Models
{
    public class InnerLoopDividerResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
        public InnerLoopDividerOperation Operation { get; set; }

        /// <summary>Populated for Operation.Analyze.</summary>
        public List<RoofLoopModel> Loops { get; set; }

        /// <summary>Populated for Operation.Apply — number of division points added, or -1 on transaction failure.</summary>
        public int PointsAdded { get; set; }
    }
}
