using Autodesk.Revit.DB;
using Revit22_Plugin.V4_02.Domain.Models;

namespace Revit22_Plugin.V4_02.Domain.Factories
{
    public static class RoofDataFactory
    {
        public static RoofData Create(RoofBase roof)
        {
            var data = new RoofData
            {
                Roof = roof,
                TopFace = GetTopFace(roof)
            };

            var editor = roof.GetSlabShapeEditor();
            foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                data.Vertices.Add(v);

            return data;
        }

        // 🔴 EXACT OLD LOGIC – DO NOT SIMPLIFY
        private static Face GetTopFace(RoofBase roof)
        {
            GeometryElement geom = roof.get_Geometry(new Options());
            Face top = null;
            double maxZ = double.MinValue;

            foreach (GeometryObject obj in geom)
            {
                if (obj is not Solid solid) continue;

                foreach (Face face in solid.Faces)
                {
                    BoundingBoxUV bb = face.GetBoundingBox();
                    if (bb == null) continue;

                    UV mid = new UV(
                        (bb.Min.U + bb.Max.U) / 2,
                        (bb.Min.V + bb.Max.V) / 2);

                    XYZ pt = face.Evaluate(mid);
                    if (pt == null) continue;

                    if (pt.Z > maxZ)
                    {
                        maxZ = pt.Z;
                        top = face;
                    }
                }
            }

            return top;
        }
    }
}
