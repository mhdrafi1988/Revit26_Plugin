using Autodesk.Revit.DB;

namespace Revit26_Plugin.AutoSlopeByDrain_06_00.Core.Engine
{
    public static class AutoSlopeGeometry
    {
        public static Face GetTopFace(RoofBase roof)
        {
            if (roof == null) return null;

            Options opt = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false
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
            }
            return topFace;
        }

        public static bool IsPointOnFace(XYZ point, Face face)
        {
            if (point == null || face == null) return false;
            IntersectionResult proj = face.Project(point);
            if (proj == null) return false;
            UV uv = proj.UVPoint;

            try
            {
                return face.IsInside(uv);
            }
            catch
            {
                BoundingBoxUV bb = face.GetBoundingBox();
                if (bb == null) return false;
                return uv.U >= bb.Min.U && uv.U <= bb.Max.U &&
                       uv.V >= bb.Min.V && uv.V <= bb.Max.V;
            }
        }
    }
}