using Autodesk.Revit.DB;

namespace Revit26_Plugin.APUS_V315.Models.Entities;

public record SheetPlacementArea(XYZ Origin, double Width, double Height)
{
    public double Bottom => Origin.Y - Height;
    public double Right => Origin.X + Width;

    public bool Contains(XYZ point) =>
        point.X >= Origin.X && point.X <= Right &&
        point.Y <= Origin.Y && point.Y >= Bottom;

    public override string ToString() =>
        $"Origin: ({Origin.X:F2}, {Origin.Y:F2}), Size: {Width:F2}′ × {Height:F2}′";
}