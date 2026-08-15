using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.SectionViewAutoTagger.V003
{
    /// <summary>
    /// Computes tag head positions along the view's alignment line (a
    /// vertical line at a fixed offset from the crop boundary — see
    /// CropBoundaryHelper), auto-stacking elements with fixed spacing so tags
    /// never overlap. Confirmed approach: elements sorted by their position
    /// along the view's "up" axis, then evenly spaced at SpacingMm apart
    /// starting from the topmost element's own elevation.
    /// </summary>
    public class TagStackLayoutService
    {
        /// <summary>
        /// One planned tag placement: the element, and the resolved head
        /// position (leader end point) in the view's model coordinates.
        /// </summary>
        public class PlannedTagPosition
        {
            public ElementId ElementId { get; }
            public XYZ HeadPosition { get; }

            public PlannedTagPosition(ElementId elementId, XYZ headPosition)
            {
                ElementId = elementId;
                HeadPosition = headPosition;
            }
        }

        /// <summary>
        /// Given elements with their reference point (e.g. element location
        /// or bounding box center, in view coordinates) and the resolved
        /// alignment line X position, returns one head position per element,
        /// stacked top-to-bottom with fixed spacing along view-up.
        /// </summary>
        /// <param name="elementReferencePoints">Element id + its natural (unstacked) point in the view plane.</param>
        /// <param name="alignmentLineX">X coordinate (in view right-direction) of the vertical alignment line, from CropBoundaryHelper.</param>
        /// <param name="spacingFeet">Fixed vertical spacing between stacked tags, converted to feet by the caller.</param>
        public List<PlannedTagPosition> ComputeStackedPositions(
            IReadOnlyList<(ElementId Id, XYZ ViewPoint)> elementReferencePoints,
            double alignmentLineX,
            double spacingFeet)
        {
            var result = new List<PlannedTagPosition>();

            // Sort top-to-bottom by the view's vertical (Y) coordinate, descending
            // so the highest element gets the topmost stack slot.
            var sorted = elementReferencePoints
                .OrderByDescending(e => e.ViewPoint.Y)
                .ToList();

            if (sorted.Count == 0)
                return result;

            double startY = sorted[0].ViewPoint.Y;

            for (int i = 0; i < sorted.Count; i++)
            {
                double stackedY = startY - (i * spacingFeet);
                var headPoint = new XYZ(alignmentLineX, stackedY, sorted[i].ViewPoint.Z);
                result.Add(new PlannedTagPosition(sorted[i].Id, headPoint));
            }

            return result;
        }
    }
}
