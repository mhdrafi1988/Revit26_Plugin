using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.RoofFromFloor.Models
{
    public class RoofMemoryContext
    {
        public ElementId RoofId { get; set; }
        public Level RoofLevel { get; set; }
        public double RoofBaseElevation { get; set; }

        public BoundingBoxXYZ BoundingBox { get; set; }

        // Flattened footprint curves (roof is authoritative)
        public List<Curve> RoofFootprintCurves { get; set; } = new();
    }
}
