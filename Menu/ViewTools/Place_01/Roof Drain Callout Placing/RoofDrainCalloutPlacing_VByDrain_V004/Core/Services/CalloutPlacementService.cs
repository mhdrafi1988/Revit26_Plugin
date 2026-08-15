using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin.RoofDrainCalloutPlacing.VByDrain.V004.Models;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.VByDrain.V004.Services
{
    /// <summary>
    /// Places one rectangular reference callout per selected opening — never a
    /// shared box across a group (confirmed with Rafi: grouping is for the UI
    /// list/sorting and per-group Auto/Fixed settings only, not for merging
    /// multiple openings into a single callout).
    ///
    /// Sizing, per opening, using its group's resolved GroupSizingSettings:
    ///   - Auto: callout box = that single opening's own geometric bounds
    ///     (from its LoopGeometry) + Margin on every side.
    ///   - Fixed: callout box = FixedSize x FixedSize square, centered on the
    ///     opening's center point.
    ///
    /// Uses ViewSection.CreateReferenceCallout — the parent view is not converted
    /// or duplicated; Revit only adds a callout marker, since the referenced
    /// drafting view already exists.
    ///
    /// ASSUMPTION (flagged, not confirmed): duplicate-callout check only compares
    /// against centers of boxes placed earlier in the SAME run — it does not query
    /// the model for pre-existing reference callouts from prior runs. A new
    /// instance of this service must be created per run (do not reuse across
    /// Execute() calls) so _placedBoxCenters starts empty each time.
    /// </summary>
    public class CalloutPlacementService
    {
        private readonly List<XYZ> _placedBoxCenters = new List<XYZ>();

        /// <summary>
        /// Places one reference callout for the given opening, sized per its
        /// group's resolved settings. Throws CalloutPlacementSkippedException if
        /// the resulting box duplicates one already placed this run, or is
        /// degenerate.
        /// </summary>
        public void PlaceReferenceCallout(
            Document doc,
            ElementId parentViewId,
            ElementId draftingViewId,
            OpeningItem opening,
            GroupSizingSettings sizing,
            double duplicateToleranceFeet)
        {
            XYZ min, max;

            if (sizing.Mode == "fixed")
            {
                double halfFeet = MmToFeet(sizing.FixedSize) / 2.0;
                var c = opening.CenterPoint;
                min = new XYZ(c.X - halfFeet, c.Y - halfFeet, 0);
                max = new XYZ(c.X + halfFeet, c.Y + halfFeet, 0);
            }
            else
            {
                double marginFeet = MmToFeet(sizing.Margin);
                var (loopMin, loopMax) = ComputeOpeningBounds(opening);
                min = new XYZ(loopMin.X - marginFeet, loopMin.Y - marginFeet, 0);
                max = new XYZ(loopMax.X + marginFeet, loopMax.Y + marginFeet, 0);
            }

            var center = new XYZ((min.X + max.X) / 2.0, (min.Y + max.Y) / 2.0, 0);

            foreach (var placed in _placedBoxCenters)
            {
                double dx = center.X - placed.X;
                double dy = center.Y - placed.Y;
                double distXY = Math.Sqrt(dx * dx + dy * dy);
                if (distXY <= duplicateToleranceFeet)
                    throw new CalloutPlacementSkippedException(
                        $"Within duplicate tolerance ({distXY * 304.8:F0}mm) of a callout box already placed this run — skipped.");
            }

            if (min.DistanceTo(max) < 1e-6)
                throw new CalloutPlacementSkippedException("Degenerate callout box (zero size) — skipped.");

            ViewSection.CreateReferenceCallout(doc, parentViewId, draftingViewId, min, max);

            _placedBoxCenters.Add(center);
        }

        /// <summary>
        /// XY bounding box of a single opening's own loop geometry (min/max over
        /// all curve tessellation points), Z flattened to 0. Falls back to a
        /// zero-spread point at the opening's center if the loop has no curves.
        /// </summary>
        private (XYZ Min, XYZ Max) ComputeOpeningBounds(OpeningItem opening)
        {
            double minX = double.MaxValue, minY = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue;
            bool any = false;

            if (opening.LoopGeometry != null)
            {
                foreach (Curve curve in opening.LoopGeometry)
                {
                    foreach (var pt in curve.Tessellate())
                    {
                        any = true;
                        if (pt.X < minX) minX = pt.X;
                        if (pt.X > maxX) maxX = pt.X;
                        if (pt.Y < minY) minY = pt.Y;
                        if (pt.Y > maxY) maxY = pt.Y;
                    }
                }
            }

            if (!any)
            {
                var c = opening.CenterPoint;
                return (new XYZ(c.X, c.Y, 0), new XYZ(c.X, c.Y, 0));
            }

            return (new XYZ(minX, minY, 0), new XYZ(maxX, maxY, 0));
        }

        private static double MmToFeet(double mm) => mm / 304.8;
    }
}
