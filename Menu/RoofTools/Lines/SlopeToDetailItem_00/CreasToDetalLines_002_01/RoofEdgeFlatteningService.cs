using Autodesk.Revit.DB;
using Revit26_Plugin.CreaserAdv_V002.Models;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class RoofEdgeFlatteningService
    {
        public IList<FlattenedEdge2D> Flatten(IList<Edge> edges)
        {
            var result = new List<FlattenedEdge2D>();

            foreach (var edge in edges)
            {
                var curve = edge.AsCurve();
                if (curve == null)
                    continue;

                var p0 = curve.GetEndPoint(0);
                var p1 = curve.GetEndPoint(1);

                var flat0 = new XYZ(p0.X, p0.Y, 0);
                var flat1 = new XYZ(p1.X, p1.Y, 0);

                // 🔒 NO CLEANUP, NO CLASSIFICATION
                result.Add(new FlattenedEdge2D(flat0, flat1, true));
            }

            return result;
        }
    }
}
