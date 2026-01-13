using Autodesk.Revit.DB;

namespace Revit26_Plugin.CSFL_V07.Models
{
    /// <summary>
    /// Represents a candidate floor/roof element near a section line,
    /// ranked by horizontal distance.
    /// </summary>
    public class CandidateHostElement
    {
        public Element Element { get; }
        public BoundingBoxXYZ BoundingBox { get; }
        public double Distance { get; }

        public CandidateHostElement(
            Element element,
            BoundingBoxXYZ boundingBox,
            double distance)
        {
            Element = element;
            BoundingBox = boundingBox;
            Distance = distance;
        }
    }
}
