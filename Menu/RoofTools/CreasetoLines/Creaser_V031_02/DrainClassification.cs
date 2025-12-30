using System.Collections.Generic;

namespace Revit26_Plugin.Creaser_V31.Models
{
    /// <summary>
    /// Semantic classification of graph nodes.
    /// </summary>
    internal sealed class DrainClassification
    {
        public HashSet<XYZKey> Corners { get; } = new();
        public HashSet<XYZKey> Ridges { get; } = new();
        public HashSet<XYZKey> Drains { get; } = new();
        public HashSet<XYZKey> OtherBoundaries { get; } = new();
    }
}
