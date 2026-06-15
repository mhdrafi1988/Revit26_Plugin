using Autodesk.Revit.DB;

namespace Revit26_Plugin.APUS_V315.Models.Entities;

public record GridCell(XYZ TopLeft, double Width, double Height)
{
    public XYZ Center => new(
        TopLeft.X + Width / 2,
        TopLeft.Y - Height / 2,
        0);

    public override string ToString() =>
        $"Cell: ({TopLeft.X:F2}, {TopLeft.Y:F2}), {Width:F2}'×{Height:F2}'";
}