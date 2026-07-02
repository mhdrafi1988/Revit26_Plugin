using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofEdgeVertexReducer.V001.Models
{
    public enum VertexAction
    {
        KeepStart,
        KeepEnd,
        KeepMaxZ,
        Remove,
        /// <summary>Not within tolerance of any straight segment — always left untouched.</summary>
        KeepUnmatched
    }

    /// <summary>
    /// Result of classifying one SlabShapeVertex against the roof's straight edges
    /// and applying the keep/remove rule.
    /// </summary>
    public class VertexDecision
    {
        public SlabShapeVertex Vertex { get; set; }
        public XYZ Position { get; set; }

        /// <summary>Elevation in Revit internal units (feet).</summary>
        public double ZFeet { get; set; }

        public string SegmentLabel { get; set; }
        public VertexAction Action { get; set; }
        public string Reason { get; set; }

        public bool WillRemove => Action == VertexAction.Remove;
    }
}
