using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Models;

namespace Revit26_Plugin.LinkedDetailLineGenerator.VA003.Core.Services
{
    /// <summary>
    /// Builds the 2D marker geometry (curve loop) centered on a transformed/projected
    /// insertion point, per spec Section 19-20. V1 UI (per approved mockup) exposes
    /// independent Circle and Rectangle settings cards (Section 4a/4b) — the mapping's
    /// RepresentationMode (Circle or Rectangle) selects which settings apply.
    ///
    /// Circle uses two half-circle Arcs (spec's documented workaround: a full 360°
    /// sweep throws in Arc.Create — two half-arcs joined at opposite points is the
    /// standard, exact solution, not an approximation).
    /// </summary>
    public class PointMarkerGeometryService
    {
        /// <summary>Builds a closed curve loop for the marker, in the same coordinate
        /// space as the given center point (already transformed to host + projected
        /// to plan by the caller).</summary>
        /// <param name="rotationRadians">Rectangle-only: angle to rotate the
        /// marker around <paramref name="center"/>, resolved by the caller from
        /// RectangleMarkerSettings.AlignmentMode (see PointProcessingEngine.
        /// ComputeRectangleRotationRadians). Ignored for Circle — a circle is
        /// rotationally symmetric.</param>
        public List<Curve> BuildMarker(
            XYZ center,
            RepresentationMode mode,
            CircleMarkerSettings circleSettings,
            RectangleMarkerSettings rectangleSettings,
            double rotationRadians = 0.0)
        {
            if (mode == RepresentationMode.Rectangle)
                return BuildRectangle(center, rectangleSettings.WidthMm, rectangleSettings.HeightMm, rotationRadians);

            return BuildCircle(center, circleSettings.DiameterMm);
        }

        /// <summary>Two half-circle Arcs joined at opposite diameter points — the
        /// documented workaround for Arc.Create throwing on a full 2pi sweep. Exact,
        /// not sampled: each half is still a true analytic Arc.</summary>
        private List<Curve> BuildCircle(XYZ center, double diameterMm)
        {
            double radiusFeet = MmToFeet(diameterMm) / 2.0;

            XYZ p0 = center + new XYZ(radiusFeet, 0, 0);
            XYZ p1 = center - new XYZ(radiusFeet, 0, 0);
            XYZ topMid = center + new XYZ(0, radiusFeet, 0);
            XYZ bottomMid = center - new XYZ(0, radiusFeet, 0);

            Arc topHalf = Arc.Create(p0, p1, topMid);
            Arc bottomHalf = Arc.Create(p1, p0, bottomMid);

            return new List<Curve> { topHalf, bottomHalf };
        }

        private List<Curve> BuildRectangle(XYZ center, double widthMm, double heightMm, double rotationRadians)
        {
            double halfW = MmToFeet(widthMm) / 2.0;
            double halfH = MmToFeet(heightMm) / 2.0;

            XYZ[] local =
            {
                new XYZ(-halfW, -halfH, 0),
                new XYZ(halfW, -halfH, 0),
                new XYZ(halfW, halfH, 0),
                new XYZ(-halfW, halfH, 0),
            };

            double cos = Math.Cos(rotationRadians);
            double sin = Math.Sin(rotationRadians);
            var corners = new XYZ[4];
            for (int i = 0; i < 4; i++)
            {
                XYZ c = local[i];
                corners[i] = center + new XYZ(c.X * cos - c.Y * sin, c.X * sin + c.Y * cos, 0);
            }

            return new List<Curve>
            {
                Line.CreateBound(corners[0], corners[1]),
                Line.CreateBound(corners[1], corners[2]),
                Line.CreateBound(corners[2], corners[3]),
                Line.CreateBound(corners[3], corners[0]),
            };
        }

        /// <summary>Revit's internal units are feet; UI marker sizes are entered in
        /// mm per the approved mockup (Section 4a/4b fields labeled "mm").</summary>
        private static double MmToFeet(double mm) => mm / 304.8;
    }
}
