using Autodesk.Revit.DB;

namespace Revit22_Plugin.RoofTagV4.Models
{
    /// <summary>
    /// Type of point selected for tagging based on V4 rules.
    /// </summary>
    public enum TagPointType
    {
        Corner,
        InnerOpening,
        Drain
    }

    /// <summary>
    /// Represents a tagging point with metadata.
    /// </summary>
    public class TagPoint
    {
        public XYZ Point { get; set; }
        public TagPointType Type { get; set; }

        // Optional: Additional metadata for debugging or filtering
        public int GroupId { get; set; } = -1;
        public double Elevation => Point?.Z ?? 0;

        public TagPoint(XYZ p, TagPointType type)
        {
            Point = p;
            Type = type;
        }

        public override string ToString()
        {
            return $"{Type} - ({Point.X:0.###}, {Point.Y:0.###}, {Point.Z:0.###})";
        }
    }
}
