using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.RoofDrainCalloutPlacing.V006.Models;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.V006.Services
{
    /// <summary>
    /// Places a rectangular reference callout sized to a cluster's own point
    /// spread in the parent plan view, pointing at the single user-selected
    /// drafting view. Uses ViewSection.CreateReferenceCallout — the parent view
    /// is not converted or duplicated; Revit only adds a callout marker, since
    /// the referenced view already exists (drafting views can be referenced
    /// from any parent view type).
    ///
    /// Box sizing (confirmed with Rafi): min/max X and Y of the cluster's own
    /// points, expanded by marginFeet on every side, then widened so neither
    /// dimension is smaller than floorFeet. A single-point cluster (zero
    /// spread) always renders at exactly floorFeet × floorFeet.
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
        /// derived per-call from clusterPoints, not a single fixed value.
        /// </summary>
        public void PlaceReferenceCallout(
            Document doc,
            ElementId parentViewId,
            ElementId draftingViewId,
            XYZ centroid,
            IList<XYZ> clusterPoints,
            double marginFeet,
            double floorFeet,
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

            var (min, max) = ComputeCalloutBox(centroid, clusterPoints, marginFeet, floorFeet);

            if (min.DistanceTo(max) < 1e-6)
                throw new CalloutPlacementSkippedException("Degenerate callout box (zero size) — skipped.");

            ViewSection.CreateReferenceCallout(doc, parentViewId, draftingViewId, min, max);

            _placedCentroids.Add(centroid);
        }

        /// <summary>
        /// Computes the callout's min/max corners: the cluster's own point
        /// bounding box (min/max X and Y across clusterPoints) expanded by
        /// marginFeet on every side, then each dimension widened — symmetric
        /// about the centroid — to at least floorFeet if the bounding box alone
        /// would be smaller. Z is flattened to 0, matching the fixed-size
        /// version's behavior (callouts sit in a plan view).
        /// </summary>
        private (XYZ min, XYZ max) ComputeCalloutBox(
            XYZ centroid, IList<XYZ> clusterPoints, double marginFeet, double floorFeet)
        {
            double minX, maxX, minY, maxY;

            if (clusterPoints == null || clusterPoints.Count == 0)
            {
                // No point data available (shouldn't normally happen — callers always
                // pass the cluster's own points) — fall back to a zero-spread box
                // centered on the centroid, which the floor widening below expands
                // to floorFeet × floorFeet.
                minX = maxX = centroid.X;
                minY = maxY = centroid.Y;
            }
            else
            {
                minX = clusterPoints.Min(p => p.X);
                maxX = clusterPoints.Max(p => p.X);
                minY = clusterPoints.Min(p => p.Y);
                maxY = clusterPoints.Max(p => p.Y);
            }

            minX -= marginFeet; maxX += marginFeet;
            minY -= marginFeet; maxY += marginFeet;

            double width = maxX - minX;
            double height = maxY - minY;

            // Widen symmetrically about the centroid (not the bounding box center)
            // so the callout stays centered on the drain cluster's true centroid,
            // matching the fixed-size version's centering behavior.
            if (width < floorFeet)
            {
                double half = floorFeet / 2.0;
                minX = centroid.X - half;
                maxX = centroid.X + half;
            }
            if (height < floorFeet)
            {
                double half = floorFeet / 2.0;
                minY = centroid.Y - half;
                maxY = centroid.Y + half;
            }

            return (new XYZ(minX, minY, 0), new XYZ(maxX, maxY, 0));
        }
    }
}

