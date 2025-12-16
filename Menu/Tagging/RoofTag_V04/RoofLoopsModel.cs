using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit22_Plugin.RoofTagV4.Models
{
    /// <summary>
    /// Packaged roof geometry set used by V4 for tagging calculations.
    /// Contains boundary, all vertices, centroid and main axis vector.
    /// </summary>
    public class RoofLoopsModel
    {
        /// <summary>
        /// All slab shape vertices (raw points from shape editor).
        /// </summary>
        public List<XYZ> AllVertices { get; set; } = new List<XYZ>();

        /// <summary>
        /// Outer boundary polygon of roof top face.
        /// </summary>
        public List<XYZ> Boundary { get; set; } = new List<XYZ>();

        /// <summary>
        /// XY centroid of all vertices.
        /// </summary>
        public XYZ Centroid { get; set; } = XYZ.Zero;

        /// <summary>
        /// Main axis vector of the roof (longest edge or PCA vector).
        /// Must be normalized.
        /// </summary>
        public XYZ MainAxis { get; set; } = XYZ.BasisX;

        public override string ToString()
        {
            return $"Vertices: {AllVertices.Count}, Boundary: {Boundary.Count}, Axis: ({MainAxis.X:0.##}, {MainAxis.Y:0.##})";
        }
    }
}
