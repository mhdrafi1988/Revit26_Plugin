using Autodesk.Revit.DB;
using Revit26_Plugin.CreaserAdv_V003.Models;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V003.Services
{
    public class RoofEdgeFlatteningService
    {
        public IList<FlattenedEdge2D> Flatten(IList<Edge> edges)
        {
            var list = new List<FlattenedEdge2D>();

            foreach (var e in edges)
            {
                var c = e.AsCurve();
                var p0 = c.GetEndPoint(0);
                var p1 = c.GetEndPoint(1);

                list.Add(new FlattenedEdge2D(
                    new XYZ(p0.X, p0.Y, 0),
                    new XYZ(p1.X, p1.Y, 0)));
            }
            return list;
        }
    }
}
