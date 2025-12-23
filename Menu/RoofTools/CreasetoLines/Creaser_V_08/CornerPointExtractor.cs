using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_V08.Commands.GeometryHelpers
{
    /// <summary>
    /// Extracts top face boundary corner points.
    /// </summary>
    public static class CornerPointExtractor
    {
        public static IList<XYZ> GetTopFaceCorners(Element roof)
        {
            List<XYZ> corners = new();

            Options opt = new()
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false
            };

            GeometryElement geom = roof.get_Geometry(opt);
            if (geom == null)
                return corners;

            foreach (GeometryObject obj in geom)
            {
                if (obj is Solid solid && solid.Faces.Size > 0)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face is PlanarFace pf && pf.FaceNormal.Z > 0.99)
                        {
                            foreach (EdgeArray loop in face.EdgeLoops)
                            {
                                foreach (Edge edge in loop)
                                {
                                    IList<XYZ> pts = edge.Tessellate();
                                    if (pts.Count > 0)
                                        corners.Add(pts[0]);
                                }
                            }
                            return corners;
                        }
                    }
                }
            }

            return corners;
        }
    }
}
