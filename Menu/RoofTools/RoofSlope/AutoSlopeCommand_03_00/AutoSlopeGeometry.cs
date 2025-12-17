using Autodesk.Revit.DB;
using System;

namespace Revit22_Plugin.AutoSlopeV3.Engines
{
    public static class AutoSlopeGeometry
    {
        public static Face GetTopFace(RoofBase roof)
        {
            try
            {
                Options opt = new Options();
                GeometryElement geom = roof.get_Geometry(opt);

                Face topFace = null;
                double maxZ = double.MinValue;

                foreach (GeometryObject obj in geom)
                {
                    Solid solid = obj as Solid;
                    if (solid == null) continue;

                    foreach (Face face in solid.Faces)
                    {
                        BoundingBoxUV bb = face.GetBoundingBox();
                        UV mid = new UV((bb.Min.U + bb.Max.U) * 0.5, (bb.Min.V + bb.Max.V) * 0.5);
                        XYZ pt = face.Evaluate(mid);

                        if (pt != null && pt.Z > maxZ)
                        {
                            maxZ = pt.Z;
                            topFace = face;
                        }
                    }
                }
                return topFace;
            }
            catch
            {
                return null;
            }
        }

        public static bool IsPointOnFace(XYZ point, Face face)
        {
            try
            {
                IntersectionResult res = face.Project(point);
                if (res == null) return false;

                UV uv = res.UVPoint;
                BoundingBoxUV bb = face.GetBoundingBox();

                return uv.U >= bb.Min.U &&
                       uv.U <= bb.Max.U &&
                       uv.V >= bb.Min.V &&
                       uv.V <= bb.Max.V;
            }
            catch
            {
                return false;
            }
        }
    }
}
