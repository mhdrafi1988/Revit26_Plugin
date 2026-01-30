using Autodesk.Revit.DB;
using Revit26_Plugin.DwgSymbolicConverter_V03.Models;

namespace Revit26_Plugin.DwgSymbolicConverter_V03.Helpers
{
    public static partial class GeometryHelper
    {
        /// <summary>
        /// Calculates tag head position aligned to VIEW (sheet) directions.
        /// </summary>
        public static XYZ CalculateTagPosition(
            XYZ anchorPoint,
            View view,
            TagPlacementCorner corner,
            TagPlacementDirection direction,
            double offsetDistance)
        {
            // View-based directions (THIS IS THE KEY)
            XYZ up = view.UpDirection.Normalize();
            XYZ right = view.RightDirection.Normalize();

            // Outward = positive offset, Inward = negative offset
            double d = direction == TagPlacementDirection.Outward
                ? offsetDistance
                : -offsetDistance;

            XYZ offset = XYZ.Zero;

            switch (corner)
            {
                case TagPlacementCorner.TopRight:
                    offset = up * d + right * d;
                    break;

                case TagPlacementCorner.TopLeft:
                    offset = up * d - right * d;
                    break;

                case TagPlacementCorner.BottomRight:
                    offset = -up * d + right * d;
                    break;

                case TagPlacementCorner.BottomLeft:
                    offset = -up * d - right * d;
                    break;
            }

            return anchorPoint + offset;
        }
    }
}
