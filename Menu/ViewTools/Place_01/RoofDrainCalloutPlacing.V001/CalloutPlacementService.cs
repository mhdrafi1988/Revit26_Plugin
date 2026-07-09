using Autodesk.Revit.DB;
using Revit26_Plugin.RoofDrainCalloutPlacing.V001.Models;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.V001.Services
{
    /// <summary>
    /// Places a rectangular reference callout at a centroid in the parent plan view,
    /// pointing at the single user-selected drafting view. Uses
    /// ViewSection.CreateReferenceCallout — the parent view is not converted or
    /// duplicated; Revit only adds a callout marker, since the referenced view
    /// already exists (drafting views can be referenced from any parent view type).
    /// </summary>
    public class CalloutPlacementService
    {
        /// <summary>
        /// calloutSizeFeet is the full width/height of the fixed rectangular callout
        /// (model space, since the parent is a ViewPlan — not sheet space, so no
        /// parentView.Scale multiplication is applied here, unlike CalloutPlacing's
        /// sheet-based offsets).
        /// </summary>
        public void PlaceReferenceCallout(
            Document doc,
            ElementId parentViewId,
            ElementId draftingViewId,
            XYZ centroid,
            double calloutSizeFeet)
        {
            double half = calloutSizeFeet / 2.0;

            var min = new XYZ(centroid.X - half, centroid.Y - half, 0);
            var max = new XYZ(centroid.X + half, centroid.Y + half, 0);

            if (min.DistanceTo(max) < 1e-6)
                throw new CalloutPlacementSkippedException("Degenerate callout box (zero size) — skipped.");

            ViewSection.CreateReferenceCallout(doc, parentViewId, draftingViewId, min, max);
        }
    }
}
