using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin.RoofDrainCalloutPlacing.V005.Models;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.V005.Services
{
    /// <summary>
    /// Places a rectangular reference callout, centered on the cluster's
    /// centroid, in the parent plan view, pointing at the single
    /// user-selected drafting view. Uses ViewSection.CreateReferenceCallout —
    /// the parent view is not converted or duplicated; Revit only adds a
    /// callout marker, since the referenced view already exists (drafting
    /// views can be referenced from any parent view type).
    ///
    /// Box sizing (V006: fixed size, no margin — confirmed with Rafi): every
    /// callout renders at exactly fixedSizeFeet × fixedSizeFeet, centered on
    /// the cluster's centroid, regardless of the cluster's own point spread.
    ///
    /// ASSUMPTION (flagged, not confirmed): duplicate-callout check only compares
    /// against centroids placed earlier in the SAME run — it does not query the
    /// model for pre-existing reference callouts from prior runs. A new instance
    /// of this service must be created per run (do not reuse across Execute() calls)
    /// so _placedCentroids starts empty each time.
    /// </summary>
    public class CalloutPlacementService
    {
        private readonly List<XYZ> _placedCentroids = new List<XYZ>();

        /// <summary>
        /// Position is always centered on the cluster's own centroid — no offset
        /// or rotation control in this version (default-settings scope). Size is
        /// always fixedSizeFeet × fixedSizeFeet — clusterPoints is retained only
        /// as a parameter for call-site/logging compatibility; it no longer
        /// affects box sizing (V006: fixed size, margin removed).
        /// </summary>
        public void PlaceReferenceCallout(
            Document doc,
            ElementId parentViewId,
            ElementId draftingViewId,
            XYZ centroid,
            IList<XYZ> clusterPoints,
            double fixedSizeFeet,
            double duplicateToleranceFeet)
        {
            foreach (var placed in _placedCentroids)
            {
                double dx = centroid.X - placed.X;
                double dy = centroid.Y - placed.Y;
                double distXY = System.Math.Sqrt(dx * dx + dy * dy);
                if (distXY <= duplicateToleranceFeet)
                    throw new CalloutPlacementSkippedException(
                        $"Within duplicate tolerance ({distXY * 304.8:F0}mm) of a callout already placed this run — skipped.");
            }

            var (min, max) = ComputeCalloutBox(centroid, fixedSizeFeet);

            if (min.DistanceTo(max) < 1e-6)
                throw new CalloutPlacementSkippedException("Degenerate callout box (zero size) — skipped.");

            ViewSection.CreateReferenceCallout(doc, parentViewId, draftingViewId, min, max);

            _placedCentroids.Add(centroid);
        }

        /// <summary>
        /// Computes the callout's min/max corners: a fixedSizeFeet × fixedSizeFeet
        /// box centered on the cluster's centroid. Z is flattened to 0, matching
        /// the plan-view placement (callouts sit in a plan view).
        /// </summary>
        private (XYZ min, XYZ max) ComputeCalloutBox(XYZ centroid, double fixedSizeFeet)
        {
            double half = fixedSizeFeet / 2.0;
            var min = new XYZ(centroid.X - half, centroid.Y - half, 0);
            var max = new XYZ(centroid.X + half, centroid.Y + half, 0);
            return (min, max);
        }
    }
}

