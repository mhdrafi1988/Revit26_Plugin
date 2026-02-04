using Autodesk.Revit.DB;
using System.Linq;

namespace Revit26_Plugin.AutoSlopeByPoint_WIP2.Helpers
{
    public static class GeometryHelper
    {
        public static Face GetTopFace(RoofBase roof)
        {
            if (roof == null) return null;

            Options options = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geometry = roof.get_Geometry(options);
            if (geometry == null) return null;

            Face topFace = null;
            double maxElevation = double.MinValue;

            foreach (GeometryObject geomObj in geometry)
            {
                if (geomObj is Solid solid && solid.Faces.Size > 0)
                {
                    foreach (Face face in solid.Faces)
                    {
                        // Get face center
                        BoundingBoxUV bbox = face.GetBoundingBox();
                        if (bbox == null) continue;

                        UV center = new UV(
                            (bbox.Min.U + bbox.Max.U) * 0.5,
                            (bbox.Min.V + bbox.Max.V) * 0.5);

                        XYZ point = face.Evaluate(center);
                        if (point == null) continue;

                        // Check if this is the highest face
                        if (point.Z > maxElevation)
                        {
                            maxElevation = point.Z;
                            topFace = face;
                        }
                    }
                }
            }

            return topFace;
        }

        public static bool IsPointOnFace(XYZ point, Face face, double tolerance = 0.001)
        {
            if (point == null || face == null)
                return false;

            IntersectionResult projection = face.Project(point);
            if (projection == null)
                return false;

            UV uvPoint = projection.UVPoint;
            XYZ projectedPoint = projection.XYZPoint;

            // Check if projected point is close to original point
            if (point.DistanceTo(projectedPoint) > tolerance)
                return false;

            try
            {
                return face.IsInside(uvPoint);
            }
            catch
            {
                // Fallback for trimmed faces
                BoundingBoxUV bbox = face.GetBoundingBox();
                if (bbox == null) return false;

                return uvPoint.U >= bbox.Min.U - tolerance &&
                       uvPoint.U <= bbox.Max.U + tolerance &&
                       uvPoint.V >= bbox.Min.V - tolerance &&
                       uvPoint.V <= bbox.Max.V + tolerance;
            }
        }
    }
}