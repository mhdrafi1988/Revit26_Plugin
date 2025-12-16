using Autodesk.Revit.DB;

namespace Revit22_Plugin.RoofTagV4.Helpers
{
    public static class TagReferenceHelperV4
    {
        /// <summary>
        /// Projects point onto roof top face and returns the face reference.
        /// This is the SAME way V3 places SpotElevation tags.
        /// </summary>
        public static bool GetFaceReference(
            RoofBase roof,
            XYZ inputPt,
            out XYZ projected,
            out Reference faceRef)
        {
            projected = null;
            faceRef = null;

            Options opt = new Options
            {
                ComputeReferences = true,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geom = roof.get_Geometry(opt);
            if (geom == null) return false;

            double bestDist = double.MaxValue;

            foreach (GeometryObject obj in geom)
            {
                Solid solid = obj as Solid;
                if (solid == null || solid.Faces.Size == 0) continue;

                foreach (Face face in solid.Faces)
                {
                    PlanarFace pf = face as PlanarFace;
                    if (pf == null) continue;

                    // Only tag top face
                    if (pf.FaceNormal.Z < 0.7) continue;

                    IntersectionResult ir = pf.Project(inputPt);
                    if (ir == null) continue;

                    double dist = inputPt.DistanceTo(ir.XYZPoint);
                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        projected = ir.XYZPoint;
                        faceRef = pf.Reference;
                    }
                }
            }

            return (projected != null && faceRef != null);
        }
    }
}
