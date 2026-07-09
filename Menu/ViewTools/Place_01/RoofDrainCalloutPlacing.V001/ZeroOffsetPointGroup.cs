using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.V001.Models
{
    /// <summary>
    /// A cluster of zero-offset SlabShapeVertex points found on one or more roofs,
    /// reduced to a single centroid where a callout will be placed.
    /// </summary>
    public class ZeroOffsetPointGroup
    {
        /// <summary>Id of the roof this group's points came from.</summary>
        public ElementId RoofId { get; set; }

        /// <summary>Raw points (internal feet) that were clustered together.</summary>
        public List<XYZ> Points { get; set; } = new List<XYZ>();

        /// <summary>Centroid of the group, XY only (Z taken from the first point / view level).</summary>
        public XYZ Centroid { get; set; }
    }
}
