using Autodesk.Revit.DB;

namespace Revit26_Plugin.APUS_V313.Models
{
    public class SheetPlacementArea
    {
        public XYZ Origin { get; }
        public double Width { get; }
        public double Height { get; }

        // Convenience properties
        public double Left => Origin.X;
        public double Right => Origin.X + Width;
        public double Top => Origin.Y;
        public double Bottom => Origin.Y - Height;

        public SheetPlacementArea(XYZ origin, double width, double height)
        {
            Origin = origin;
            Width = width;
            Height = height;
        }
    }
}