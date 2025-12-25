using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_V100.Models
{
    public class CreaseSegment
    {
        public XYZ Start { get; }
        public XYZ End { get; }
        public double Length { get; }

        public CreaseSegment(XYZ start, XYZ end)
        {
            Start = start;
            End = end;
            Length = start.DistanceTo(end);
        }
    }
}
