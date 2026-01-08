using Autodesk.Revit.DB;
using Revit26_Plugin.V5_00.Domain.Models;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.V5_00.Infrastructure.Revit
{
    public static class AutoSlopeGeometry
    {
        public static RoofData BuildRoofData(RoofBase roof)
        {
            var data = new RoofData
            {
                Roof = roof,
                TopFace = GetTopFace(roof),
                Vertices = GetShapeVertices(roof)
            };

            return data;
        }

        private static Face GetTopFace(RoofBase roof)
        {
            Options opt = new Options
            {
                DetailLevel = ViewDetailLevel.Fine,
                ComputeReferences = true
            };

            GeometryElement geom = roof.get_Geometry(opt);

            foreach (GeometryObject obj in geom)
            {
                if (obj is Solid solid)
                {
                    foreach (Face f in solid.Faces)
                    {
                        XYZ n = f.ComputeNormal(new UV(0.5, 0.5));
                        if (n != null && n.Z > 0.9)
                            return f;
                    }
                }
            }

            return null;
        }

        private static List<SlabShapeVertex> GetShapeVertices(RoofBase roof)
        {
            var editor = roof.GetSlabShapeEditor();
            var vertices = new List<SlabShapeVertex>();
            var vertexArray = editor?.SlabShapeVertices;
            if (vertexArray != null)
            {
                for (int i = 0; i < vertexArray.Size; i++)
                {
                    var vertex = vertexArray.get_Item(i);
                    if (vertex != null)
                        vertices.Add(vertex);
                }
            }
            return vertices;
        }
    }
}
