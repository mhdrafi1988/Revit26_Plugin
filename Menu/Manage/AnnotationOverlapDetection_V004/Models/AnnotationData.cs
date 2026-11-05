using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.AnnotationOverlapDetection.V004.Models
{
    /// <summary>A 2D point in view coordinates (mm along the view's right / up axes).</summary>
    internal readonly record struct Pt2(double X, double Y);

    /// <summary>
    /// Internal working data for a single annotation element, collected during
    /// Step 2 (Annotation Collection). Coordinates are stored in model mm, view coordinates.
    /// Not bound to the UI directly - OverlapResult is the UI-facing model.
    /// </summary>
    internal class AnnotationData
    {
        public ElementId ElementId { get; set; }
        public string TypeName { get; set; }

        /// <summary>View the annotation was collected in (v4.2.0: a scan can cover several views).</summary>
        public ElementId ViewId { get; set; }

        /// <summary>Name of that view.</summary>
        public string ViewName { get; set; }

        /// <summary>Scale of that view (e.g. 100 for 1:100).</summary>
        public int ViewScale { get; set; } = 1;

        // Axis-aligned bounding box in mm, view coordinate system
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public double Right => X + Width;
        public double Bottom => Y + Height;

        /// <summary>
        /// v4.2.0: outline used for the overlap test - 4 corners, counter-clockwise.
        /// For rotated text and tags this is the rotated rectangle, which is tighter than the
        /// axis-aligned box. Null means "use the axis-aligned box".
        /// </summary>
        public IReadOnlyList<Pt2> Outline { get; set; }
    }
}
