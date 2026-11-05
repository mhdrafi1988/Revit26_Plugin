using System;
using System.Collections.Generic;
using Revit26_Plugin.AnnotationOverlapDetection.V004.Models;

namespace Revit26_Plugin.AnnotationOverlapDetection.V004.Helpers
{
    /// <summary>
    /// Convex polygon helpers for the overlap test (v4.2.0). Pure math, no Revit API.
    /// </summary>
    internal static class PolygonMath
    {
        /// <summary>Overlap areas at or below this (mm²) count as "touching", not overlapping.</summary>
        private const double AreaEpsilon = 1e-9;

        /// <summary>Angles closer than this to 45° cannot be solved reliably from a bounding box.</summary>
        private const double MinCos2Theta = 0.2;

        /// <summary>The 4 corners of an axis-aligned box, counter-clockwise.</summary>
        public static List<Pt2> Rectangle(double x, double y, double w, double h)
            => new() { new(x, y), new(x + w, y), new(x + w, y + h), new(x, y + h) };

        /// <summary>The outline of an annotation: its rotated outline if it has one, else its box.</summary>
        public static IReadOnlyList<Pt2> OutlineOf(AnnotationData a)
            => a.Outline ?? Rectangle(a.X, a.Y, a.Width, a.Height);

        /// <summary>
        /// Recovers the rotated rectangle inside an axis-aligned box, given its rotation angle.
        /// Solves W = w|cos| + h|sin| and H = w|sin| + h|cos| for w and h.
        /// Returns null when the angle is ~0°/90° (the box is already exact), near 45°
        /// (cannot be solved), or the result is not a valid rectangle.
        /// </summary>
        /// <param name="knownWidth">Optional rectangle width (mm); used to solve near 45°.</param>
        public static List<Pt2> RotatedRectangleInBox(double x, double y, double boxW, double boxH,
            double angleRad, double? knownWidth = null)
        {
            double c = Math.Abs(Math.Cos(angleRad));
            double s = Math.Abs(Math.Sin(angleRad));
            if (c < 1e-6 || s < 1e-6)
                return null; // 0° or 90°: axis-aligned box is exact

            double w, h;
            double det = c * c - s * s;
            if (Math.Abs(det) >= MinCos2Theta)
            {
                w = (boxW * c - boxH * s) / det;
                h = (boxH * c - boxW * s) / det;
            }
            else if (knownWidth.HasValue && knownWidth.Value > 0)
            {
                w = knownWidth.Value;
                h = c >= s ? (boxH - w * s) / c : (boxW - w * c) / s;
            }
            else
            {
                return null;
            }

            if (w <= 0 || h <= 0 || w > boxW + boxH || h > boxW + boxH)
                return null;

            double cx = x + boxW / 2, cy = y + boxH / 2;
            double ux = Math.Cos(angleRad), uy = Math.Sin(angleRad); // along the text
            double vx = -uy, vy = ux;                                // up the text
            double hw = w / 2, hh = h / 2;

            return new List<Pt2>
            {
                new(cx - ux * hw - vx * hh, cy - uy * hw - vy * hh),
                new(cx + ux * hw - vx * hh, cy + uy * hw - vy * hh),
                new(cx + ux * hw + vx * hh, cy + uy * hw + vy * hh),
                new(cx - ux * hw + vx * hh, cy - uy * hw + vy * hh)
            };
        }

        /// <summary>
        /// Intersection of two convex polygons (Sutherland–Hodgman). Empty when they do not overlap.
        /// </summary>
        public static List<Pt2> Intersect(IReadOnlyList<Pt2> subject, IReadOnlyList<Pt2> clip)
        {
            var output = new List<Pt2>(subject);
            bool ccw = SignedArea(clip) >= 0;

            for (int i = 0; i < clip.Count && output.Count > 0; i++)
            {
                Pt2 a = clip[i], b = clip[(i + 1) % clip.Count];
                var input = output;
                output = new List<Pt2>();

                for (int j = 0; j < input.Count; j++)
                {
                    Pt2 p = input[j], q = input[(j + 1) % input.Count];
                    bool pIn = Inside(a, b, p, ccw), qIn = Inside(a, b, q, ccw);
                    if (pIn)
                    {
                        output.Add(p);
                        if (!qIn) output.Add(LineIntersect(a, b, p, q));
                    }
                    else if (qIn)
                    {
                        output.Add(LineIntersect(a, b, p, q));
                    }
                }
            }

            return output;
        }

        /// <summary>
        /// Overlap of two outlines: whether the overlap area is > 0, and the width / height of the
        /// overlap region's axis-aligned box (mm).
        /// </summary>
        public static (bool overlaps, double width, double height) Overlap(IReadOnlyList<Pt2> a, IReadOnlyList<Pt2> b)
        {
            var poly = Intersect(a, b);
            if (poly.Count < 3 || Math.Abs(SignedArea(poly)) <= AreaEpsilon)
                return (false, 0, 0);

            double minX = double.MaxValue, maxX = double.MinValue, minY = double.MaxValue, maxY = double.MinValue;
            foreach (var p in poly)
            {
                minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
                minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
            }
            return (true, maxX - minX, maxY - minY);
        }

        /// <summary>Signed area (positive when counter-clockwise).</summary>
        public static double SignedArea(IReadOnlyList<Pt2> poly)
        {
            double sum = 0;
            for (int i = 0; i < poly.Count; i++)
            {
                Pt2 p = poly[i], q = poly[(i + 1) % poly.Count];
                sum += p.X * q.Y - q.X * p.Y;
            }
            return sum / 2;
        }

        private static bool Inside(Pt2 a, Pt2 b, Pt2 p, bool ccw)
        {
            double cross = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
            return ccw ? cross >= 0 : cross <= 0;
        }

        private static Pt2 LineIntersect(Pt2 a, Pt2 b, Pt2 p, Pt2 q)
        {
            double a1 = b.Y - a.Y, b1 = a.X - b.X, c1 = a1 * a.X + b1 * a.Y;
            double a2 = q.Y - p.Y, b2 = p.X - q.X, c2 = a2 * p.X + b2 * p.Y;
            double det = a1 * b2 - a2 * b1;
            if (Math.Abs(det) < 1e-12)
                return p;
            return new Pt2((b2 * c1 - b1 * c2) / det, (a1 * c2 - a2 * c1) / det);
        }
    }
}
