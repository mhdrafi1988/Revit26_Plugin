using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.V006.Helpers
{
    /// <summary>
    /// Restricts Selection.PickObjects(ObjectType.PointOnElement, ...) to
    /// references on one specific roof (the roof already picked earlier in
    /// the command). AllowElement gates which elements are even considered;
    /// AllowReference then further restricts to references whose owning
    /// element is that exact roof instance — a wall or a different roof in
    /// the background is rejected even though AllowElement alone can't tell
    /// "this roof" from "some other roof" without the reference's element id.
    /// </summary>
    public class RoofFaceSelectionFilter : ISelectionFilter
    {
        private readonly ElementId _roofId;

        public RoofFaceSelectionFilter(ElementId roofId)
        {
            _roofId = roofId;
        }

        public bool AllowElement(Element element) => element.Id == _roofId;

        public bool AllowReference(Reference reference, XYZ position) => reference.ElementId == _roofId;
    }
}
