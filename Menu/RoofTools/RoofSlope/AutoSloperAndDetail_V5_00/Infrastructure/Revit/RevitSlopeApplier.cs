using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.V5_00.Infrastructure.Revit
{
    public static class RevitSlopeApplier
    {
        public static void ApplyVertexOffsets(
            RoofBase roof,
            Dictionary<SlabShapeVertex, double> vertexOffsets)
        {
            var editor = roof.GetSlabShapeEditor();

            foreach (var kvp in vertexOffsets)
            {
                editor.ModifySubElement(kvp.Key, kvp.Value);
            }
        }
    }
}
