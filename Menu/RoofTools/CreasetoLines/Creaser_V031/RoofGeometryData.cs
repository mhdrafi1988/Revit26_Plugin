using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_V31.Models
{
    /// <summary>
    /// Raw roof geometry extracted from Revit.
    /// No business logic.
    /// </summary>
    public sealed class RoofGeometryData  // Changed from internal to public
    {
        public HashSet<XYZKey> BoundaryNodes { get; } = new();
        public Dictionary<XYZKey, List<XYZKey>> DownhillGraph { get; } = new();
    }
}