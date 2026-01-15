using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    /// <summary>
    /// Calculates signed polygon area using Shoelace formula.
    /// Works strictly in 2D (XY).
    /// </summary>
    internal static class LoopAreaCalculator
    {
        public static double ComputeArea(IList<FlattenedEdge2D> edges)
        {
            double area = 0;

            for (int i = 0; i < edges.Count; i++)
            {
                XYZ p1 = edges[i].Start2D;
                XYZ p2 = edges[i].End2D;

                area += (p1.X * p2.Y) - (p2.X * p1.Y);
            }

            return area * 0.5;
        }
    }
}
