// File: IntersectionService.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.Services.Geometry
//
// Responsibility:
// - Computes line-to-curve intersections
// - Finds closest valid intersection point
// - Tolerance-safe geometry handling

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofRidgeLines_V06.Services.Geometry
{
    public class IntersectionService
    {
        private const double Tolerance = 1e-6;

        /// <summary>
        /// Finds the closest intersection point between a ray and boundary curves.
        /// </summary>
        public XYZ FindClosestIntersection(
            XYZ origin,
            XYZ direction,
            IList<Curve> boundaryCurves)
        {
            XYZ closestPoint = null;
            double minDistance = double.MaxValue;

            Line ray = Line.CreateUnbound(origin, direction);

            foreach (Curve boundary in boundaryCurves)
            {
                if (boundary.Intersect(ray, out IntersectionResultArray results)
                    != SetComparisonResult.Overlap)
                    continue;

                foreach (IntersectionResult result in results)
                {
                    XYZ pt = result.XYZPoint;
                    double dist = pt.DistanceTo(origin);

                    if (dist > Tolerance && dist < minDistance)
                    {
                        minDistance = dist;
                        closestPoint = pt;
                    }
                }
            }

            if (closestPoint == null)
                throw new InvalidOperationException("No intersection found with roof boundary.");

            return closestPoint;
        }
    }
}
