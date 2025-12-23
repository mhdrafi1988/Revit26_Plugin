using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_V08.Commands.GeometryHelpers
{
    /// <summary>
    /// Extracts all shape-editing points from a roof.
    /// </summary>
    public static class ShapeEditingPointExtractor
    {
        public static IList<XYZ> GetShapePoints(Element roof)
        {
            List<XYZ> points = new();

            if (roof is not RoofBase roofBase)
                return points;

            SlabShapeEditor editor = roofBase.GetSlabShapeEditor();
            if (editor == null || !editor.IsEnabled)
                return points;

            foreach (SlabShapeVertex v in editor.SlabShapeVertices)
            {
                points.Add(v.Position);
            }

            return points;
        }
    }
}
