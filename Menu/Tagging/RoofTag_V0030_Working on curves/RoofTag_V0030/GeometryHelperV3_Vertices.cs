using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofTag_V03.Helpers
{
    internal static partial class GeometryHelperV3
    {
        public static List<XYZ> GetExactShapeVertices(RoofBase roof)
        {
            if (roof == null)
                throw new ArgumentNullException(nameof(roof));

            SlabShapeEditor editor = roof.GetSlabShapeEditor();
            if (editor == null || !editor.IsEnabled)
                return new List<XYZ>();

            const double tol = 1e-6;

            return editor.SlabShapeVertices
                .Cast<SlabShapeVertex>()
                .GroupBy(v => (
                    Math.Round(v.Position.X / tol),
                    Math.Round(v.Position.Y / tol)))
                .Select(g => g.OrderByDescending(v => v.Position.Z).First().Position)
                .ToList();
        }
    }
}
