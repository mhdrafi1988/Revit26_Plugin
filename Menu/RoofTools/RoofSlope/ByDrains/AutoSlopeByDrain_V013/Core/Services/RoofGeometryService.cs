// =======================================================
// File: RoofGeometryService.cs
// Namespace: Revit26_Plugin_Revised.MultiRoofSlopeByDrain.V013.Core.Services
// Changes vs V012:
//   - InitializeRoofGeometry (called at pick time) replaced by
//     ResetShapePoints, called by the engine at the start of each roof's
//     run, inside that roof's TransactionGroup. A failed or cancelled roof
//     therefore rolls the reset back too.
//   - Shape editing is enabled only when it is off.
// =======================================================

using Autodesk.Revit.DB;
using System;

namespace Revit26_Plugin_Revised.MultiRoofSlopeByDrain.V013.Core.Services
{
    public static class RoofGeometryService
    {
        /// <summary>
        /// Turns shape editing on when needed and sets every shape point to 0.
        /// Must be called outside an open Transaction (it opens its own).
        /// Returns the number of points reset.
        /// </summary>
        public static int ResetShapePoints(RoofBase roof, Document doc)
        {
            int count = 0;
            using (Transaction tx = new Transaction(doc, "AutoSlope By Drain - Reset Shape Points"))
            {
                tx.Start();

                SlabShapeEditor editor = roof.GetSlabShapeEditor()
                    ?? throw new InvalidOperationException("This roof type does not support shape editing.");

                if (!editor.IsEnabled)
                    editor.Enable();

                foreach (SlabShapeVertex vertex in editor.SlabShapeVertices)
                {
                    editor.ModifySubElement(vertex, 0.0);
                    count++;
                }

                tx.Commit();
            }
            return count;
        }

        public static Face GetTopFace(RoofBase roof)
        {
            GeometryElement geomElem = roof.get_Geometry(new Options());
            if (geomElem == null) return null;

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
