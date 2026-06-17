using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.PerpendicularPointoDrain.V01.Models
{
    /// <summary>
    /// One cluster of drain points that fell within the grouping tolerance of each other,
    /// collapsed down to a single centroid used as the reference point for boundary search.
    /// </summary>
    public class DrainGroupModel
    {
        public string    Label       { get; set; }   // "G1", "G2", ...
        public List<XYZ> DrainPoints { get; set; }
        public XYZ       Centroid    { get; set; }
        public int       DrainCount  => DrainPoints?.Count ?? 0;
    }
}
