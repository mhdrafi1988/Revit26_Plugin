using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V306.ViewModels;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V306.Services
{
    /// <summary>
    /// Sorts section views by their marker position projected
    /// into a reference view coordinate system.
    /// Reading order: Top ? Bottom, Left ? Right.
    /// </summary>
    public static class SectionSortingService
    {
        public static List<SectionItemViewModel> Sort(
            IEnumerable<SectionItemViewModel> items,
            View referenceView)
        {
            // Defensive check
            if (referenceView == null)
                return items.ToList();

            XYZ origin = referenceView.Origin;
            XYZ right = referenceView.RightDirection;
            XYZ up = referenceView.UpDirection;

            return items
                .Select(item =>
                {
                    XYZ modelPoint = GetMarkerPoint(item.View);

                    // Project model point into view coordinates
                    XYZ v = modelPoint - origin;

                    double x = v.DotProduct(right);
                    double y = v.DotProduct(up);

                    return new
                    {
                        Item = item,
                        X = x,
                        Y = y
                    };
                })
                // PRIMARY: higher Y first (top)
                .OrderByDescending(p => p.Y)
                // SECONDARY: lower X first (left)
                .ThenBy(p => p.X)
                .Select(p => p.Item)
                .ToList();
        }

        /// <summary>
        /// Gets a stable marker position for a section view.
        /// </summary>
        private static XYZ GetMarkerPoint(ViewSection view)
        {
            // Preferred: section marker curve
            if (view.Location is LocationCurve lc)
            {
                // Midpoint is more stable than endpoint
                return lc.Curve.Evaluate(0.5, true);
            }

            // Fallback: crop box center
            BoundingBoxXYZ bb = view.CropBox;
            return (bb.Min + bb.Max) * 0.5;
        }
    }
}
