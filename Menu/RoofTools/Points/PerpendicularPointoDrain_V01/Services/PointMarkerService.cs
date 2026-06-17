using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.PerpendicularPointoDrain.V01.Services
{
    /// <summary>
    /// Draws a small flat circle (two arcs, no solid) as a DirectShape at a point — purely a
    /// visual marker. SlabShapeVertex points created by editor.AddPoint have no visible
    /// representation outside Edit Shape mode, so this gives an always-visible confirmation
    /// of where each point actually landed.
    /// </summary>
    public static class PointMarkerService
    {
        public static void CreateCircleMarker(Document doc, XYZ center, double radiusMm)
        {
            if (doc == null || center == null) return;

            double radiusFt = UnitUtils.ConvertToInternalUnits(radiusMm, UnitTypeId.Millimeters);

            // Flat, horizontal circle at the point's elevation — not tilted to match
            // roof slope. Two semicircle arcs, since Revit's Arc type can't represent
            // a closed full circle as a single curve.
            Arc arc1 = Arc.Create(center, radiusFt, 0, Math.PI, XYZ.BasisX, XYZ.BasisY);
            Arc arc2 = Arc.Create(center, radiusFt, Math.PI, 2 * Math.PI, XYZ.BasisX, XYZ.BasisY);

            var geometry = new List<GeometryObject> { arc1, arc2 };

            DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
            ds.SetShape(geometry);
            ds.Name = "Drain Projection Point Marker";
        }
    }
}
