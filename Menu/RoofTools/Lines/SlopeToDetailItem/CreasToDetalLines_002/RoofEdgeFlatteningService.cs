using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    public class FlattenedEdge2D
    {
        public XYZ Start2D { get; }
        public XYZ End2D { get; }

        public FlattenedEdge2D(XYZ start, XYZ end)
        {
            Start2D = new XYZ(start.X, start.Y, 0);
            End2D = new XYZ(end.X, end.Y, 0);
        }
    }

    public class RoofEdgeFlatteningService
    {
        public IList<FlattenedEdge2D> Flatten(IEnumerable<Edge> edges)
        {
            var result = new List<FlattenedEdge2D>();

            foreach (Edge edge in edges)
            {
                Curve curve = edge.AsCurve();
                XYZ p0 = curve.GetEndPoint(0);
                XYZ p1 = curve.GetEndPoint(1);

                result.Add(new FlattenedEdge2D(p0, p1));
            }

            return result;
        }
    }
}
