using Autodesk.Revit.DB;
using System;

namespace Revit26_Plugin.AutoSlope.V5_00.Core.Engine
{
    public static class GeometryHelper
    {
        public static Face GetTopFace(RoofBase roof)
        {
            if (roof == null) return null;

            Options opt = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Medium
            };

            GeometryElement geom = roof.get_Geometry(opt);
            if (geom == null) return null;

            Face topFace = null;
            double maxZ = double.MinValue;

            foreach (GeometryObject obj in geom)
            {
                if (obj is not Solid solid || solid.Faces.Size == 0)
                    continue;

                foreach (Face face in solid.Faces)
                {
                    try
                    {
                        BoundingBoxUV bb = face.GetBoundingBox();
                        if (bb == null) continue;

                        UV mid = new UV(
                            (bb.Min.U + bb.Max.U) * 0.5,
                            (bb.Min.V + bb.Max.V) * 0.5);

                        XYZ p = face.Evaluate(mid);
                        if (p == null) continue;

                        if (p.Z > maxZ)
                        {
                            maxZ = p.Z;
                            topFace = face;
                        }
                    }
                    catch { }
                }
            }
            return topFace;
        }

        public static bool IsPointOnFace(XYZ point, Face face, double tolerance = 0.00328084)
        {
            if (point == null || face == null) return false;

            try
            {
                IntersectionResult proj = face.Project(point);
                if (proj == null)
                {
                    proj = face.Project(point + XYZ.BasisZ * tolerance)
                        ?? face.Project(point - XYZ.BasisZ * tolerance);
                    if (proj == null) return false;
                }

                return face.IsInside(proj.UVPoint);
            }
            catch
            {
                try
                {
                    BoundingBoxUV bb = face.GetBoundingBox();
                    if (bb == null) return false;

                    var proj = face.Project(point);
                    if (proj == null) return false;

                    UV uv = proj.UVPoint;
                    return uv.U >= bb.Min.U && uv.U <= bb.Max.U &&
                           uv.V >= bb.Min.V && uv.V <= bb.Max.V;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}