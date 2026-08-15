using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.VByDrain.V004.Models
{
    /// <summary>
    /// Detected opening (inner loop from roof face) on the selected roof.
    /// Inner loops are the voids/holes cut into the roof geometry.
    /// Includes loop curves, dimensions, and checkbox selection state.
    /// Sorted by size (perimeter/diameter) within each shape type.
    /// </summary>
    public partial class OpeningItem : ObservableObject
    {
        /// <summary>The CurveLoop representing this opening's boundary on the roof face.</summary>
        public CurveLoop LoopGeometry { get; set; }
        
        public XYZ CenterPoint { get; set; }
        
        /// <summary>Enum: Circle, Rectangle, Square, Other</summary>
        public OpeningShape ShapeType { get; set; }
        
        /// <summary>Loop index (0-based) for identification; useful for logging.</summary>
        public string LoopIdentifier { get; set; }
        
        /// <summary>Width in mm — valid for Rectangle/Square only.</summary>
        public double Width { get; set; }
        
        /// <summary>Height in mm — valid for Rectangle/Square only.</summary>
        public double Height { get; set; }
        
        /// <summary>Perimeter in mm — all shapes.</summary>
        public double Perimeter { get; set; }
        
        /// <summary>Diameter in mm — circles only. For rectangles/squares, may be diagonal or longest dimension for sorting.</summary>
        public double Diameter { get; set; }
        
        /// <summary>Area in mm² — for sorting within shape types.</summary>
        public double Area { get; set; }
        
        /// <summary>User-toggleable selection for callout placement.</summary>
        [ObservableProperty]
        private bool isSelected = true;

        public OpeningItem(
            CurveLoop loopGeometry,
            XYZ center,
            OpeningShape shape,
            string loopIdentifier,
            double width,
            double height,
            double perimeter,
            double diameter,
            double area)
        {
            LoopGeometry = loopGeometry;
            CenterPoint = center;
            ShapeType = shape;
            LoopIdentifier = loopIdentifier;
            Width = width;
            Height = height;
            Perimeter = perimeter;
            Diameter = diameter;
            Area = area;
        }
    }

    /// <summary>
    /// Shape classification for grouping in the UI.
    /// </summary>
    public enum OpeningShape
    {
        Circle,
        Rectangle,
        Square,
        Other
    }
}
