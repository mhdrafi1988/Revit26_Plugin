using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_V31.Models
{
    /// <summary>
    /// Fully classified drainage graph used for path solving.
    /// </summary>
    public sealed class DrainGraphData  // Changed from internal to public
    {
        public Dictionary<XYZKey, List<XYZKey>> Graph { get; init; }
        public HashSet<XYZKey> CornerNodes { get; init; }
        public HashSet<XYZKey> RidgeNodes { get; init; }
        public HashSet<XYZKey> DrainNodes { get; init; }
        public HashSet<XYZKey> OtherBoundaryNodes { get; init; }
    }
}