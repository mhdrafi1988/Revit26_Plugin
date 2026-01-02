using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    /// <summary>
    /// Represents a closed 2D edge loop.
    /// </summary>
    public class EdgeLoop2D
    {
        public IList<FlattenedEdge2D> Edges { get; }
            = new List<FlattenedEdge2D>();
    }
}
