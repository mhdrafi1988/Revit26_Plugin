using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofTag_V73.Helpers
{
    internal static partial class GeometryHelper
    {
        public static bool GetTaggingReferenceOnRoof(
            Element roof,
            XYZ inputPoint,
            out Reference faceReference,
            out XYZ projectedPoint)
        {
            faceReference = null;
            projectedPoint = null;

            Options options = new()
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false
            };

            GeometryElement geomElem = roof.get_Geometry(options);
            double minDistance = double.MaxValue;

            foreach (GeometryObject obj in geomElem)
            {
                if (obj is not Solid solid || solid.Faces.IsEmpty)
                    continue;

                foreach (Face face in solid.Faces)
                {
                    IntersectionResult result = face.Project(inputPoint);
                    if (result == null) continue;

                    XYZ p = result.XYZPoint;
                    UV uv = result.UVPoint;
                    XYZ normal = face.ComputeNormal(uv);

                    if (normal.Z < 0.2) continue;

                    double dist = inputPoint.DistanceTo(p);
                    if (dist >= minDistance) continue;

                    minDistance = dist;
                    faceReference = face.Reference;
                    projectedPoint = p;
                }
            }

            return faceReference != null;
        }
    }
}
