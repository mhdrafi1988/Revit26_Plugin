using Autodesk.Revit.DB;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Services
{
    public enum ElementLocationKind { Point, Curve, Unsupported }

    /// <summary>
    /// Determines whether an element has a reliable Point location, a reliable
    /// Curve (LocationCurve) location, or neither — per spec Section 21's
    /// mechanical-family auto-detection requirement:
    ///
    ///   "Mechanical equipment must NOT automatically be classified as Point-Based.
    ///    Determine whether the family has: Point location, Curve location, Other
    ///    reliable representation. If neither is available, skip it in V1 and report it."
    ///
    /// This classifier is used for ALL Point-group categories (not just Mechanical
    /// Equipment) as the authoritative per-element check at processing time — the
    /// category a user checked in the tree (Section 2) determines candidacy, but this
    /// classifier determines actual representation. Structural/Architectural Columns
    /// are near-universally Point-located in practice, but are still run through the
    /// same check rather than assumed, so a column family with an unusual host-based
    /// or curve-based definition is still handled correctly rather than silently
    /// mis-rendered.
    /// </summary>
    public class ElementLocationClassifier
    {
        public ElementLocationKind Classify(Element element)
        {
            if (element.Location is LocationPoint)
                return ElementLocationKind.Point;

            if (element.Location is LocationCurve locCurve && locCurve.Curve != null && locCurve.Curve.Length > 1e-6)
                return ElementLocationKind.Curve;

            return ElementLocationKind.Unsupported;
        }
    }
}
