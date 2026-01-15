using Autodesk.Revit.DB;

namespace Revit26_Plugin.CreaserAdv_V003.Models
{
    public class FlattenedEdge2D
    {
        public XYZ Start { get; }
        public XYZ End { get; }

        public FlattenedEdge2D(XYZ start, XYZ end)
        {
            Start = start;
            End = end;
        }

        public Line ToLine(double elevation)
        {
            return Line.CreateBound(
                new XYZ(Start.X, Start.Y, elevation),
                new XYZ(End.X, End.Y, elevation));
        }
    }
}
