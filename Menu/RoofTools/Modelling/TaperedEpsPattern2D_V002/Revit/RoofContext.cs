using Autodesk.Revit.DB;
using Revit26_Plugin_Revised.TaperedEpsPattern2D.V002.Core.Pattern;

namespace Revit26_Plugin_Revised.TaperedEpsPattern2D.V002.Revit
{
    /// <summary>
    /// Snapshot of the picked roof and view, read in a valid API context so the modeless
    /// window never has to call the Revit API itself.
    /// </summary>
    public sealed class RoofContext
    {
        public RoofContext(Document document, ElementId viewId, ElementId roofId)
        {
            Document = document;
            ViewId = viewId;
            RoofId = roofId;
        }

        public Document Document { get; }
        public ElementId ViewId { get; }
        public ElementId RoofId { get; }

        public string ViewName { get; init; } = string.Empty;
        public string RoofName { get; init; } = string.Empty;
        public string LevelName { get; init; } = string.Empty;

        /// <summary>Elevation of the view's level relative to the internal origin (ft).</summary>
        public double LevelElevationFeet { get; init; }

        /// <summary>Revit's ShortCurveTolerance (ft).</summary>
        public double ShortCurveToleranceFeet { get; init; }

        /// <summary>Minimum segment length for the Core engine: max(1 mm, ShortCurveTolerance) in metres.</summary>
        public double MinSegmentLengthMetres { get; init; } = GeometryTolerance.MinSegmentLength;

        public int SketchEdgeCount { get; init; }

        public OutlineValidationResult Outline { get; init; } = new OutlineValidationResult();

        public bool IsValid => Outline.IsValid;

        public string GroupName => $"EPS2D_{RoofId.Value}_{ViewId.Value}";
    }
}
