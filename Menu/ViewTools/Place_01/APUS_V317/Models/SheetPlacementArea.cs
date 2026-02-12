// File: SheetPlacementArea.cs
using Autodesk.Revit.DB;

namespace Revit26_Plugin.APUS_V317.Models
{
    public class SheetPlacementArea
    {
        public XYZ Origin { get; }
        public double Width { get; }
        public double Height { get; }
        public double Bottom => Origin.Y - Height;
        public double Right => Origin.X + Width;

        public SheetPlacementArea(XYZ origin, double width, double height)
        {
            Origin = origin;
            Width = width;
            Height = height;
        }

        public bool Contains(XYZ point)
        {
            return point.X >= Origin.X && point.X <= Right &&
                   point.Y <= Origin.Y && point.Y >= Bottom;
        }

        public override string ToString()
        {
            return $"Origin: ({Origin.X:F2}, {Origin.Y:F2}), Size: {Width:F2} × {Height:F2} ft";
        }
    }
}