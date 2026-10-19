// File: RoofGeometryService.cs
// Location: Core/Services/
//
// Extracted from AutoSlopeDrainCommand — shape-editor initialization and
// top-face lookup are Revit-glue steps, not command orchestration, so they
// belong in a Service.

using Autodesk.Revit.DB;

namespace Revit26_Plugin.AutoSlopeByDrain.V007.Core.Services
{
    public static class RoofGeometryService
    {
        public static void InitializeRoofGeometry(RoofBase roof, Document doc)
        {
            using (Transaction tx = new Transaction(doc, "AutoSlope By Drain - Initialize Geometry"))
            {
                tx.Start();

                var slabShapeEditor = roof.GetSlabShapeEditor();
                if (!slabShapeEditor.IsEnabled)
                {
                    slabShapeEditor.Enable();
                }

                foreach (SlabShapeVertex vertex in slabShapeEditor.SlabShapeVertices)
                {
                    slabShapeEditor.ModifySubElement(vertex, 0.0);
                }

                tx.Commit();
            }
        }

        public static Face GetTopFace(RoofBase roof)
        {
            GeometryElement geomElem = roof.get_Geometry(new Options());
            Face topFace = null;
            double maxZ = double.MinValue;

            foreach (GeometryObject geomObj in geomElem)
            {
                if (geomObj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face == null) continue;
                        BoundingBoxUV bb = face.GetBoundingBox();
                        if (bb == null) continue;

                        UV midpointUV = new UV((bb.Min.U + bb.Max.U) / 2, (bb.Min.V + bb.Max.V) / 2);
                        XYZ midpoint = face.Evaluate(midpointUV);

                        if (midpoint != null && midpoint.Z > maxZ)
                        {
                            maxZ = midpoint.Z;
                            topFace = face;
                        }
                    }
                }
            }
            return topFace;
        }
    }
}
