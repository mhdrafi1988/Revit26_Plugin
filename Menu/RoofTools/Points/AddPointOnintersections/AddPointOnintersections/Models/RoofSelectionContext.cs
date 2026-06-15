using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.AddPointOnintersections.Models
{
    public class RoofSelectionContext
    {
        public RoofSelectionContext(
            UIDocument uiDocument,
            ElementId activeViewId,
            ElementId roofId,
            IReadOnlyList<ElementId> detailLineIds)
        {
            UiDocument = uiDocument;
            ActiveViewId = activeViewId;
            RoofId = roofId;
            DetailLineIds = detailLineIds;
        }

        public UIDocument UiDocument { get; }
        public ElementId ActiveViewId { get; }
        public ElementId RoofId { get; }
        public IReadOnlyList<ElementId> DetailLineIds { get; }
    }
}