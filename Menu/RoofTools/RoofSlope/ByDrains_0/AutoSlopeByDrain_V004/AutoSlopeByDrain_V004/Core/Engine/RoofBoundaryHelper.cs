// File: RoofBoundaryHelper.cs
// Location: Core/Engine/
// V004 addition: ported from AutoSlopeByPoint's AutoSlopeGeometry.GetBoundaryArcs
// (that class also has GetTopFace/IsPointOnFace, but ByDrain already has its own
// equivalents elsewhere — only the arc-extraction piece was missing here).

using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByDrain.V004.Core.Engine
{
    public static class RoofBoundaryHelper
    {
        /// <summary>
        /// Returns every Arc curve found on the top face's boundary loops —
        /// outer boundary AND inner loops (openings/drains with curved edges).
        /// Non-arc curves (Line, etc.) are skipped since only arcs need
        /// arc-length-aware (tangent-route) handling in DijkstraPathEngine.
        /// </summary>
        public static List<Arc> GetBoundaryArcs(Face topFace)
        {
            var arcs = new List<Arc>();
            if (topFace == null) return arcs;

            foreach (EdgeArray loop in topFace.EdgeLoops)
            {
                foreach (Edge edge in loop)
                {
                    if (edge.AsCurve() is Arc arc)
                        arcs.Add(arc);
                }
            }
            return arcs;
        }
    }
}
