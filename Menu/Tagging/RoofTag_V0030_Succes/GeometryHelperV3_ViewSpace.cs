﻿using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofTag_V03.Helpers
{
    internal static partial class GeometryHelperV3
    {
        /// <summary>
        /// Gets the view-relative position of a point in the active view
        /// </summary>
        public static ViewRelativePosition GetViewRelativePosition(
            View view,
            XYZ point,
            BoundingBoxXYZ elementBBox = null)
        {
            // Transform point to view coordinates
            Transform transform = view.CropBox.Transform.Inverse;
            XYZ pointView = transform.OfPoint(point);

            // Get view's right and up directions
            XYZ right = view.RightDirection.Normalize();
            XYZ up = view.UpDirection.Normalize();

            // If no element bounding box provided, use view crop box
            BoundingBoxXYZ bbox = elementBBox ?? view.CropBox;
            XYZ center = (bbox.Min + bbox.Max) * 0.5;

            // Calculate relative position
            XYZ delta = pointView - center;
            double x = delta.DotProduct(right);
            double y = delta.DotProduct(up);

            // Determine quadrant
            bool isRight = x >= 0;
            bool isTop = y >= 0;

            // Determine horizontal and vertical position relative to center
            double absX = Math.Abs(x);
            double absY = Math.Abs(y);

            HorizontalPosition horizontalPos = isRight ? 
                HorizontalPosition.Right : HorizontalPosition.Left;
            VerticalPosition verticalPos = isTop ? 
                VerticalPosition.Top : VerticalPosition.Bottom;

            return new ViewRelativePosition
            {
                Horizontal = horizontalPos,
                Vertical = verticalPos,
                IsRight = isRight,
                IsTop = isTop,
                NormalizedX = x,
                NormalizedY = y
            };
        }

        /// <summary>
        /// Gets movement directions based on placement mode and position
        /// </summary>
        public static (XYZ verticalDir, XYZ horizontalDir) GetMovementDirections(
            View view,
            ViewRelativePosition position,
            PlacementMode placementMode)
        {
            // Get basis vectors in view space
            XYZ right = view.RightDirection.Normalize();
            XYZ up = view.UpDirection.Normalize();

            // Transform to model space for final output
            Transform transform = view.CropBox.Transform;

            XYZ verticalDir;
            XYZ horizontalDir;

            if (placementMode == PlacementMode.Outward)
            {
                // OUTWARD: Move away from element center
                verticalDir = position.IsTop ? up : -up;
                horizontalDir = position.IsRight ? right : -right;
            }
            else // Inward
            {
                // INWARD: Move toward element center (reverse both directions)
                verticalDir = position.IsTop ? -up : up;      // Opposite of outward
                horizontalDir = position.IsRight ? -right : right; // Opposite of outward
            }

            // Convert to model coordinates
            verticalDir = transform.OfVector(verticalDir).Normalize();
            horizontalDir = transform.OfVector(horizontalDir).Normalize();

            return (verticalDir, horizontalDir);
        }

        /// <summary>
        /// Validates that the end point is positioned correctly relative to bounding box
        /// </summary>
        public static bool ValidateEndPointPosition(
            View view,
            XYZ anchorPoint,
            XYZ endPoint,
            BoundingBoxXYZ bbox,
            PlacementMode placementMode)
        {
            ViewRelativePosition anchorPos = GetViewRelativePosition(view, anchorPoint, bbox);
            ViewRelativePosition endPos = GetViewRelativePosition(view, endPoint, bbox);

            if (placementMode == PlacementMode.Outward)
            {
                // End point should be further from center than anchor point
                double anchorDist = Math.Abs(anchorPos.NormalizedX) + Math.Abs(anchorPos.NormalizedY);
                double endDist = Math.Abs(endPos.NormalizedX) + Math.Abs(endPos.NormalizedY);

                return endDist > anchorDist;
            }
            else // Inward
            {
                // End point should be closer to center than anchor point
                double anchorDist = Math.Abs(anchorPos.NormalizedX) + Math.Abs(anchorPos.NormalizedY);
                double endDist = Math.Abs(endPos.NormalizedX) + Math.Abs(endPos.NormalizedY);

                return endDist < anchorDist;
            }
        }

        /// <summary>
        /// Gets the bounding box of selected points in view coordinates
        /// </summary>
        public static BoundingBoxXYZ GetPointsBoundingBoxInView(List<XYZ> points, View view)
        {
            if (points == null || points.Count == 0)
                return null;

            Transform transform = view.CropBox.Transform.Inverse;

            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

            foreach (XYZ point in points)
            {
                XYZ pointView = transform.OfPoint(point);
                minX = Math.Min(minX, pointView.X);
                minY = Math.Min(minY, pointView.Y);
                minZ = Math.Min(minZ, pointView.Z);
                maxX = Math.Max(maxX, pointView.X);
                maxY = Math.Max(maxY, pointView.Y);
                maxZ = Math.Max(maxZ, pointView.Z);
            }

            // Transform back to model coordinates
            Transform inverseTransform = transform.Inverse;

            return new BoundingBoxXYZ
            {
                Min = inverseTransform.OfPoint(new XYZ(minX, minY, minZ)),
                Max = inverseTransform.OfPoint(new XYZ(maxX, maxY, maxZ))
            };
        }
    }

    /// <summary>
    /// Represents a point's position relative to view and bounding box
    /// </summary>
    public class ViewRelativePosition
    {
        public HorizontalPosition Horizontal { get; set; }
        public VerticalPosition Vertical { get; set; }
        public bool IsRight { get; set; }
        public bool IsTop { get; set; }
        public double NormalizedX { get; set; }
        public double NormalizedY { get; set; }
    }

    public enum HorizontalPosition
    {
        Left,
        Right,
        Center
    }

    public enum VerticalPosition
    {
        Top,
        Bottom,
        Center
    }
}