// File: GridCell.cs
using Autodesk.Revit.DB;

namespace Revit26_Plugin.APUS_V318.Models
{
    public class GridCell
    {
        public XYZ TopLeft { get; }
        public double Width { get; }
        public double Height { get; }

        public GridCell(XYZ topLeft, double width, double height)
        {
            TopLeft = topLeft;
            Width = width;
            Height = height;
        }

        public XYZ Center =>
            new XYZ(
                TopLeft.X + Width / 2,
                TopLeft.Y - Height / 2,
                0);

        public override string ToString()
        {
            return $"Cell: {TopLeft}, W: {Width:F2}, H: {Height:F2}";
        }
    }
}