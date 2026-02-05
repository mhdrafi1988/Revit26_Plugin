﻿using Autodesk.Revit.DB;
using System;

namespace Revit26_Plugin.RoofTag_V03.Helpers
{
    internal static partial class GeometryHelperV3
    {
        internal enum PlacementMode
        {
            Inward,
            Outward
        }

        /// <summary>
        /// Determines the position of a point relative to bounding box center in view space
        /// </summary>
        private static (bool isTop, bool isRight) GetViewQuadrant(
            XYZ anchorPoint,
            XYZ center,
            XYZ rightDirection,
            XYZ upDirection)
        {
            XYZ delta = anchorPoint - center;
            double x = delta.DotProduct(rightDirection); // left/right
            double y = delta.DotProduct(upDirection);    // top/bottom

            return (y >= 0, x >= 0); // isTop, isRight
        }

        /// <summary>
        /// Computes two-step leader placement:
        /// 1) Diagonal move to BEND point
        /// 2) Orthogonal move to END point
        /// </summary>
        /// <returns>(Bend point, End point) in model coordinates</returns>
        public static (XYZ Bend, XYZ End) ComputeTwoStepLeaderPlacement(
            View view,
            Element element,
            XYZ anchorPoint,
            double bendOffsetFt,    // Already in feet
            double endOffsetFt,     // Already in feet
            PlacementMode placementMode)
        {
            // ==============================================
            // STEP 0: Get view basis and transform to view space
            // ==============================================
            XYZ right = view.RightDirection.Normalize();
            XYZ up = view.UpDirection.Normalize();
            Transform viewTransform = view.CropBox.Transform.Inverse;

            // ==============================================
            // STEP 1: Get element bounding box in view space
            // ==============================================
            BoundingBoxXYZ bbox = GetElementViewBoundingBox(element, view);
            if (bbox == null)
                return (anchorPoint, anchorPoint); // Fallback

            XYZ center = (bbox.Min + bbox.Max) * 0.5;

            // Transform anchor point to view coordinates
            XYZ anchorView = viewTransform.OfPoint(anchorPoint);

            // ==============================================
            // STEP 2: Determine position relative to bounding box
            // ==============================================
            var (isTop, isRight) = GetViewQuadrant(anchorView, center, right, up);

            // ==============================================
            // STEP 3: Calculate DIAGONAL direction for BEND movement
            // ==============================================
            XYZ diagonalDirView = CalculateDiagonalDirection(isTop, isRight, placementMode);

            // Normalize the diagonal direction
            diagonalDirView = diagonalDirView.Normalize();

            // Transform back to model coordinates
            XYZ diagonalDirModel = viewTransform.Inverse.OfVector(diagonalDirView).Normalize();

            // ==============================================
            // STEP 4: Calculate BEND point (anchor + diagonal movement)
            // ==============================================
            XYZ bendPoint = anchorPoint + diagonalDirModel * bendOffsetFt;

            // ==============================================
            // STEP 5: Calculate ORTHOGONAL direction for END movement
            // ==============================================
            XYZ orthogonalDirView = CalculateOrthogonalDirection(isTop, isRight, placementMode);

            // Normalize the orthogonal direction
            orthogonalDirView = orthogonalDirView.Normalize();

            // Transform back to model coordinates
            XYZ orthogonalDirModel = viewTransform.Inverse.OfVector(orthogonalDirView).Normalize();

            // ==============================================
            // STEP 6: Calculate END point (bend + orthogonal movement)
            // ==============================================
            XYZ endPoint = bendPoint + orthogonalDirModel * endOffsetFt;

            return (bendPoint, endPoint);
        }

        /// <summary>
        /// Calculates diagonal direction based on position and placement mode
        /// 
        /// DIRECTION MAPPING TABLE:
        /// Position      | Inward Direction | Outward Direction
        /// ----------------------------------------------------
        /// TOP + RIGHT   | (-Right + -Up)   | (+Right + +Up)
        /// TOP + LEFT    | (+Right + -Up)   | (-Right + +Up)
        /// BOTTOM + RIGHT| (-Right + +Up)   | (+Right + -Up)
        /// BOTTOM + LEFT | (+Right + +Up)   | (-Right + -Up)
        /// </summary>
        private static XYZ CalculateDiagonalDirection(bool isTop, bool isRight, PlacementMode placementMode)
        {
            // Basis vectors in view space
            XYZ right = new XYZ(1, 0, 0);  // X direction in view space
            XYZ up = new XYZ(0, 1, 0);     // Y direction in view space

            if (placementMode == PlacementMode.Outward)
            {
                // OUTWARD: Move away from center
                if (isTop && isRight) return right + up;        // ↗
                if (isTop && !isRight) return -right + up;      // ↖
                if (!isTop && isRight) return right + -up;      // ↘
                if (!isTop && !isRight) return -right + -up;    // ↙
            }
            else // Inward
            {
                // INWARD: Move toward center (opposite of outward)
                if (isTop && isRight) return -right + -up;      // ↙
                if (isTop && !isRight) return right + -up;      // ↘
                if (!isTop && isRight) return -right + up;      // ↖
                if (!isTop && !isRight) return right + up;      // ↗
            }

            // Default fallback
            return right;
        }

        /// <summary>
        /// Calculates orthogonal direction for end movement
        /// 
        /// ORTHOGONAL DIRECTION MAPPING:
        /// 
        /// OUTWARD PLACEMENT:
        /// • TOP items:     Right side → Right, Left side → Left
        /// • BOTTOM items:  Right side → Right, Left side → Left
        /// 
        /// INWARD PLACEMENT:
        /// • TOP items:     Right side → Left, Left side → Right
        /// • BOTTOM items:  Right side → Left, Left side → Right
        /// </summary>
        private static XYZ CalculateOrthogonalDirection(bool isTop, bool isRight, PlacementMode placementMode)
        {
            // Basis vector in view space
            XYZ right = new XYZ(1, 0, 0);

            if (placementMode == PlacementMode.Outward)
            {
                // OUTWARD: Use same horizontal direction as position
                return isRight ? right : -right;
            }
            else // Inward
            {
                // INWARD: Reverse horizontal direction
                return isRight ? -right : right;
            }
        }

        /// <summary>
        /// Validates that the end point respects placement mode constraints
        /// </summary>
        public static bool ValidateLeaderPlacement(
            View view,
            XYZ anchorPoint,
            XYZ bendPoint,
            XYZ endPoint,
            Element element,
            PlacementMode placementMode)
        {
            BoundingBoxXYZ bbox = GetElementViewBoundingBox(element, view);
            if (bbox == null) return false;

            Transform viewTransform = view.CropBox.Transform.Inverse;
            XYZ anchorView = viewTransform.OfPoint(anchorPoint);
            XYZ endView = viewTransform.OfPoint(endPoint);
            XYZ center = (bbox.Min + bbox.Max) * 0.5;

            // Calculate distances from center
            XYZ anchorDelta = anchorView - center;
            XYZ endDelta = endView - center;

            double anchorDist = anchorDelta.GetLength();
            double endDist = endDelta.GetLength();

            if (placementMode == PlacementMode.Outward)
            {
                // End point should be further from center than anchor point
                return endDist > anchorDist;
            }
            else // Inward
            {
                // End point should be closer to center than anchor point
                return endDist < anchorDist;
            }
        }

        /// <summary>
        /// Gets the visible bounding box of an element in view coordinates
        /// </summary>
        private static BoundingBoxXYZ GetElementViewBoundingBox(Element element, View view)
        {
            Options options = new Options
            {
                View = view,
                ComputeReferences = true
            };

            GeometryElement geometry = element.get_Geometry(options);
            if (geometry == null) return null;

            BoundingBoxXYZ bbox = geometry.GetBoundingBox();
            if (bbox == null) return null;

            // Transform to view coordinates
            Transform transform = view.CropBox.Transform;
            XYZ min = transform.Inverse.OfPoint(bbox.Min);
            XYZ max = transform.Inverse.OfPoint(bbox.Max);

            return new BoundingBoxXYZ
            {
                Min = min,
                Max = max
            };
        }

        /// <summary>
        /// Legacy method for backward compatibility - uses simple diagonal movement
        /// </summary>
        public static XYZ ComputeBendPoint(
            XYZ anchorPoint,
            XYZ centroid,
            double bendOffsetFt,
            bool bendInward)
        {
            if (centroid == null) return anchorPoint;

            XYZ direction = bendInward ?
                (centroid - anchorPoint).Normalize() :
                (anchorPoint - centroid).Normalize();

            if (direction == null) return anchorPoint;

            return anchorPoint + direction * bendOffsetFt;
        }

        /// <summary>
        /// Legacy method for backward compatibility
        /// </summary>
        public static XYZ ComputeEndPointWithAngle(
            XYZ anchorPoint,
            XYZ bendPoint,
            double angleDegrees,
            double endOffsetFt,
            XYZ outwardDirection,
            bool bendInward)
        {
            if (outwardDirection == null) return bendPoint;

            double angleRad = angleDegrees * Math.PI / 180.0;
            XYZ direction = outwardDirection.Normalize();

            if (direction == null) return bendPoint;

            // Rotate direction by specified angle
            XYZ rotatedDir = new XYZ(
                direction.X * Math.Cos(angleRad) - direction.Y * Math.Sin(angleRad),
                direction.X * Math.Sin(angleRad) + direction.Y * Math.Cos(angleRad),
                0);

            if (bendInward)
                rotatedDir = -rotatedDir;

            return bendPoint + rotatedDir * endOffsetFt;
        }

        /// <summary>
        /// Simple helper to convert UI mm to feet for direct use
        /// </summary>
        public static double ConvertMmToFeet(double millimeters)
        {
            return millimeters / 304.8;
        }
    }
}