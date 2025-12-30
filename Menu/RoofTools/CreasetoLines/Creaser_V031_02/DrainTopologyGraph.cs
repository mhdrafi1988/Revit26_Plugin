using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_V31.Models
{
    /// <summary>
    /// Pure adjacency graph with no semantic meaning.
    /// </summary>
    internal sealed class DrainTopologyGraph
    {
        public Dictionary<XYZKey, List<XYZKey>> Adjacency { get; } = new();
    }
}
