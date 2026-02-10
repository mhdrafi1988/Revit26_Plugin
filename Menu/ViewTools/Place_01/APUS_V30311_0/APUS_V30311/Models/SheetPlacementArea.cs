using Autodesk.Revit.DB;

namespace Revit26_Plugin.APUS_V311.Models
{
    public class SheetPlacementArea
    {
        public XYZ Origin { get; }
        public double Width { get; }
        public double Height { get; }

        public SheetPlacementArea(XYZ origin, double width, double height)
        {
            Origin = origin;
            Width = width;
            Height = height;
        }
    }
}
