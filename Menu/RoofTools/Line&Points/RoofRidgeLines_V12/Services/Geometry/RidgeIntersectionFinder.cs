using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Services.Geometry
{
    public class RidgeIntersectionFinder : IRidgeIntersectionFinder
    {
        public IList<XYZ> FindIntersections(RoofBase roof, Line ray)
        {
            var results = new List<XYZ>();
            var geo = roof.get_Geometry(new Options());

            foreach (Solid solid in geo.OfType<Solid>())
            {
                foreach (Edge edge in solid.Edges)
                {
                    if (edge.AsCurve().Intersect(ray,
                        out IntersectionResultArray arr)
                        == SetComparisonResult.Overlap)
                    {
                        results.Add(arr.get_Item(0).XYZPoint);
                    }
                }
            }

            return results;
        }
    }
}
