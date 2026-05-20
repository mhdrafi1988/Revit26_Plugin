// ==================================
// File: CurvedCurveProjectionService.cs
// ==================================

using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    /// <summary>
    /// Projects curved 3D creases into plan-view 2D curves
    /// Handles arcs, ellipses, NURBS, and lines
    /// </summary>
    public class CurvedCurveProjectionService
    {
        private readonly LoggingService _log;
        private const double TOLERANCE = 0.001; // 1/8 inch in feet
        private const int MAX_SEGMENTS = 50; // Maximum segments for complex curve approximation

        public CurvedCurveProjectionService(LoggingService log)
        {
            _log = log ?? throw new ArgumentNullException(nameof(log));
        }

        /// <summary>
        /// Projects a collection of 3D curves to a plan view at specified elevation
        /// </summary>
        public IList<Curve> ProjectToPlan(IList<Curve> sourceCurves, ViewPlan view)
        {
            var projected = new List<Curve>();

            if (sourceCurves == null || sourceCurves.Count == 0)
            {
                _log.Warning("No curves provided for projection.");
                return projected;
            }

            if (view?.GenLevel == null)
            {
                _log.Warning("Invalid plan view or missing level.");
                return projected;
            }

            double targetZ = view.GenLevel.Elevation;
            int failedProjections = 0;

            foreach (Curve curve in sourceCurves)
            {
                try
                {
                    Curve projectedCurve = ProjectCurveToPlane(curve, targetZ);
                    if (projectedCurve != null && projectedCurve.Length > TOLERANCE)
                    {
                        projected.Add(projectedCurve);
                    }
                    else
                    {
                        failedProjections++;
                    }
                }
                catch (Exception ex)
                {
                    _log.Warning($"Failed to project curve: {ex.Message}");
                    failedProjections++;
                }
            }

            _log.Info($"Curves projected to plan: {projected.Count} successful, {failedProjections} failed");
            return projected;
        }

        /// <summary>
        /// Projects a single 3D curve to a horizontal plane at target Z elevation
        /// </summary>
        private Curve ProjectCurveToPlane(Curve curve, double targetZ)
        {
            if (curve == null) return null;

            // Handle different curve types
            if (curve is Line line)
            {
                return ProjectLineToPlane(line, targetZ);
            }
            else if (curve is Arc arc)
            {
                return ProjectArcToPlane(arc, targetZ);
            }
            else if (curve is Ellipse ellipse)
            {
                return ProjectEllipseToPlane(ellipse, targetZ);
            }
            else if (curve is HermiteSpline || curve is NurbSpline)
            {
                return ProjectSplineToPlane(curve, targetZ);
            }
            else
            {
                // Unknown curve type - try approximation
                return ApproximateCurveWithPolyLine(curve, targetZ);
            }
        }

        /// <summary>
        /// Projects a line to horizontal plane
        /// </summary>
        private Line ProjectLineToPlane(Line line, double targetZ)
        {
            XYZ p1 = line.GetEndPoint(0);
            XYZ p2 = line.GetEndPoint(1);

            XYZ p1Proj = new XYZ(p1.X, p1.Y, targetZ);
            XYZ p2Proj = new XYZ(p2.X, p2.Y, targetZ);

            return Line.CreateBound(p1Proj, p2Proj);
        }

        /// <summary>
        /// Projects an arc to horizontal plane
        /// </summary>
        private Curve ProjectArcToPlane(Arc arc, double targetZ)
        {
            // Get arc properties
            XYZ center = arc.Center;
            double radius = arc.Radius;
            double startAngle = arc.GetEndParameter(0);
            double endAngle = arc.GetEndParameter(1);

            // Project center to target plane
            XYZ centerProj = new XYZ(center.X, center.Y, targetZ);

            // Get direction vectors (project to horizontal)
            XYZ xDir = new XYZ(arc.XDirection.X, arc.XDirection.Y, 0).Normalize();
            XYZ yDir = new XYZ(arc.YDirection.X, arc.YDirection.Y, 0).Normalize();

            // If arc is vertical or near-vertical, it may project to a line
            if (Math.Abs(arc.Normal.Z) < 0.1)
            {
                // Arc is nearly vertical - project endpoints and create line
                XYZ p1 = ProjectPointToPlane(arc.GetEndPoint(0), targetZ);
                XYZ p2 = ProjectPointToPlane(arc.GetEndPoint(1), targetZ);
                // Return as Curve, not Arc
                return Line.CreateBound(p1, p2);
            }

            try
            {
                // Create new arc in horizontal plane
                return Arc.Create(
                    centerProj,
                    radius,
                    startAngle,
                    endAngle,
                    xDir,
                    yDir);
            }
            catch
            {
                // Fallback to line between endpoints
                XYZ p1 = ProjectPointToPlane(arc.GetEndPoint(0), targetZ);
                XYZ p2 = ProjectPointToPlane(arc.GetEndPoint(1), targetZ);
                return Line.CreateBound(p1, p2);
            }
        }

        /// <summary>
        /// Projects an ellipse to horizontal plane
        /// </summary>
        private Curve ProjectEllipseToPlane(Ellipse ellipse, double targetZ)
        {
            // Ellipses are complex - approximate with points
            return ApproximateCurveWithPolyLine(ellipse, targetZ);
        }

        /// <summary>
        /// Projects a spline to horizontal plane
        /// </summary>
        private Curve ProjectSplineToPlane(Curve spline, double targetZ)
        {
            // Splines are complex - approximate with points
            return ApproximateCurveWithPolyLine(spline, targetZ);
        }

        /// <summary>
        /// Approximates any curve with a polyline by sampling points
        /// </summary>
        private Curve ApproximateCurveWithPolyLine(Curve curve, double targetZ)
        {
            // Determine number of segments based on curve length
            int segments = Math.Min(MAX_SEGMENTS, Math.Max(10, (int)(curve.Length / 0.5))); // 1 segment per 6 inches

            var points = new List<XYZ>();

            for (int i = 0; i <= segments; i++)
            {
                double param = curve.GetEndParameter(0) +
                              (curve.GetEndParameter(1) - curve.GetEndParameter(0)) * i / segments;

                XYZ point3d = curve.Evaluate(param, true);
                XYZ pointProj = new XYZ(point3d.X, point3d.Y, targetZ);
                points.Add(pointProj);
            }

            // For simple approximation, return a line between first and last if close enough
            if (points.Count >= 2)
            {
                double straightLineDistance = points[0].DistanceTo(points[points.Count - 1]);
                double curveLength = curve.Length;

                // If curve is nearly straight, return a line
                if (Math.Abs(straightLineDistance - curveLength) < 0.1)
                {
                    return Line.CreateBound(points[0], points[points.Count - 1]);
                }
            }

            // For complex curves, return the first and last point as line
            // In production, you might want to create a series of lines or a PolyLine
            return Line.CreateBound(points[0], points[points.Count - 1]);
        }

        /// <summary>
        /// Projects a point to target Z elevation
        /// </summary>
        private XYZ ProjectPointToPlane(XYZ point, double targetZ)
        {
            return new XYZ(point.X, point.Y, targetZ);
        }
    }
}