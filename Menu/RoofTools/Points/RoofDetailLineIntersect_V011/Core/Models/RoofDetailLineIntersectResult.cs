// =======================================================
// File: RoofDetailLineIntersectResult.cs
// Location: Core/Models/
// New in V011. Plain result object returned by
// RoofDetailLineIntersectEngine. No UI, no WPF, no ViewModel references.
// =======================================================

namespace Revit26_Plugin.RoofDetailLineIntersect.V011.Core.Models
{
    public class RoofDetailLineIntersectResult
    {
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public int IntersectionsFound { get; set; }
        public int PointsPlaced { get; set; }
        public int SkippedCount { get; set; }
    }
}
