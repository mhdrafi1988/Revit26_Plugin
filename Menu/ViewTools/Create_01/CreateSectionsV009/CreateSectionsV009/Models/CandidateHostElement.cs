using Autodesk.Revit.DB;

namespace Revit26_Plugin.CreateSectionsFromDetailLines.V009.Models
{
    /// <summary>
    /// Which of the 4 checked Host Source categories a candidate was
    /// collected from. V009: new — previously the caller had to
    /// re-derive this after the fact (via Document.IsLinked + Category),
    /// which was inference rather than a tracked fact. Now set once,
    /// at collection time, by HostElementSearchService — the only place
    /// that actually knows which category/pass produced the candidate.
    /// </summary>
    public enum HostSourceCategory
    {
        FloorHost,
        RoofHost,
        FloorLinked,
        RoofLinked
    }

    /// <summary>
    /// Represents a candidate floor/roof element near a section line,
    /// ranked by horizontal distance.
    ///
    /// V008: unchanged from V07.
    /// V009: added SourceCategory — used by the "Sections To Create"
    /// preview grid's Host Type column instead of re-deriving it.
    /// </summary>
    public class CandidateHostElement
    {
        public Element Element { get; }
        public BoundingBoxXYZ BoundingBox { get; }
        public double Distance { get; }
        public HostSourceCategory SourceCategory { get; }

        public CandidateHostElement(
            Element element,
            BoundingBoxXYZ boundingBox,
            double distance,
            HostSourceCategory sourceCategory)
        {
            Element = element;
            BoundingBox = boundingBox;
            Distance = distance;
            SourceCategory = sourceCategory;
        }
    }
}
