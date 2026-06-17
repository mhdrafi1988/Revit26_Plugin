using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V60.Models
{
    public enum OpeningShapeType
    {
        Rectangle,
        Circle,
        Other
    }

    /// <summary>
    /// Represents one inner loop (void/opening) of the roof slab.
    /// Holds both geometric data and UI selection state.
    /// </summary>
    public class OpeningData
    {
        /// <summary>Index in the original list of inner loops (for reference).</summary>
        public int Index { get; set; }

        /// <summary>Flattened 2D vertices of the loop (Z=0).</summary>
        public List<XYZ> Vertices { get; set; } = new List<XYZ>();

        /// <summary>Detected shape type: Rectangle, Circle, or Other.</summary>
        public OpeningShapeType ShapeType { get; set; }

        /// <summary>
        /// For Rectangle: Width (X extent). For Circle: Radius. For Other: Width of bounding box.
        /// </summary>
        public double Dim1 { get; set; }

        /// <summary>
        /// For Rectangle: Height (Y extent). For Circle: 0 (not used). For Other: Height of bounding box.
        /// </summary>
        public double Dim2 { get; set; }

        /// <summary>Bounding‑box width (full X extent).</summary>
        public double BBoxWidth { get; set; }

        /// <summary>Bounding‑box height (full Y extent).</summary>
        public double BBoxHeight { get; set; }

        /// <summary>Centroid = centre of the bounding box (per your requirement).</summary>
        public XYZ Centroid { get; set; }

        /// <summary>True if the user selects this loop as a drainage seed.</summary>
        public bool IsSelected { get; set; }
    }
}