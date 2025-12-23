using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_V08.Commands.GeometryHelpers
{
    /// <summary>
    /// Normalizes line direction for duplicate detection.
    /// </summary>
    public static class LineNormalizationHelper
    {
        public static Line Normalize(Line line)
        {
            XYZ p0 = line.GetEndPoint(0);
            XYZ p1 = line.GetEndPoint(1);

            if (p0.X < p1.X) return line;
            if (p0.X > p1.X) return Line.CreateBound(p1, p0);

            if (p0.Y < p1.Y) return line;
            if (p0.Y > p1.Y) return Line.CreateBound(p1, p0);

            if (p0.Z <= p1.Z) return line;
            return Line.CreateBound(p1, p0);
        }
    }
}
