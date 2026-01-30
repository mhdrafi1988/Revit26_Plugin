using Autodesk.Revit.DB;
using Revit26_Plugin.DwgSymbolicConverter_V03.Models;
using Revit26_Plugin.RoofTag_V73.Models;
using System;
using System.Linq;

namespace Revit26_Plugin.RoofTag_V73.Helpers
{
    /// <summary>
    /// Geometry utilities used for roof tagging.
    /// </summary>
    public static partial class GeometryHelper
    {
        /// <summary>
        /// Returns the centroid of the roof top face, projected to the view plane.
        /// </summary>
        public static XYZ GetRoofTopFaceCentroid(
            RoofBase roof,
            Reference topFaceRef,
            Document doc)
        {
            Face face = doc.GetElement(topFaceRef)
                           .GetGeometryObjectFromReference(topFaceRef) as Face;

            if (face == null)
                throw new InvalidOperationException("Unable to resolve roof top face.");

            BoundingBoxUV bb = face.GetBoundingBox();
            UV mid = (bb.Min + bb.Max) * 0.5;

            return face.Evaluate(mid);
        }

        /// <summary>
        /// Calculates a tag position aligned to VIEW directions (sheet-aligned).
        /// </summary>
        public static XYZ CalculateTagPosition(
            XYZ anchorPoint,
            View view,
            TagPlacementCorner corner,
            TagPlacementDirection direction,
            double offsetDistance)
        {
            XYZ up = view.UpDirection.Normalize();
            XYZ right = view.RightDirection.Normalize();

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
