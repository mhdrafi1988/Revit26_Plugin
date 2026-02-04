using Autodesk.Revit.DB;
using System;

namespace Revit26_Plugin.RoofTag_V03.Helpers
{
    /// <summary>
    /// Geometry helper utilities for roof tagging operations.
    /// PARTIAL class – other logic lives in other files.
    /// </summary>
    internal static partial class GeometryHelperV3
    {
        public static bool GetTaggingReferenceOnRoof(
            Element roof,
            XYZ inputPoint,
            out Reference faceReference,
            out XYZ projectedPoint)
        {
            faceReference = null;
            projectedPoint = null;

            if (roof == null || inputPoint == null)
                return false;

            Options options = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = false
            };

            GeometryElement geomElem = roof.get_Geometry(options);
            if (geomElem == null)
                return false;

            double minDistance = double.MaxValue;

            foreach (GeometryObject obj in geomElem)
            {
                if (obj is not Solid solid || solid.Faces.IsEmpty)
                    continue;

                foreach (Face face in solid.Faces)
                {
                    IntersectionResult result = face.Project(inputPoint);
                    if (result == null)
                        continue;

                    XYZ p = result.XYZPoint;
                    UV uv = result.UVPoint;
                    if (p == null || uv == null)
                        continue;

                    XYZ normal;
                    try { normal = face.ComputeNormal(uv); }
                    catch { continue; }

                    if (normal.Z < 0.2)
                        continue;

                    double dist = inputPoint.DistanceTo(p);
                    if (dist >= minDistance)
                        continue;

                    minDistance = dist;
                    faceReference = face.Reference;
                    projectedPoint = p;
                }
            }

            return faceReference != null;
        }
    }
}
