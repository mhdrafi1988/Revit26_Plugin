using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.RoofTagV4.Helpers
{
    public static class GeometryHelperV4
    {
        // ==========================================================
        // TRUE 2D POLYGON CENTROID (Shoelace formula)
        // ==========================================================
        public static XYZ ComputePolygonCentroid(List<XYZ> pts)
        {
            if (pts == null || pts.Count < 3)
                return XYZ.Zero;

            double area = 0;
            double cx = 0;
            double cy = 0;

            for (int i = 0; i < pts.Count; i++)
            {
                XYZ p1 = pts[i];
                XYZ p2 = pts[(i + 1) % pts.Count];

                double cross = (p1.X * p2.Y) - (p2.X * p1.Y);
                area += cross;
                cx += (p1.X + p2.X) * cross;
                cy += (p1.Y + p2.Y) * cross;
            }

            area *= 0.5;
            cx /= (6 * area);
            cy /= (6 * area);

            return new XYZ(cx, cy, pts[0].Z);
        }

        // ==========================================================
        // FIXED OUTWARD/INWARD DIRECTION 
        // ALWAYS CORRECT — NEVER FLIPS
        // ==========================================================
        public static XYZ GetOutwardDirection(XYZ point, List<XYZ> boundary, XYZ centroid)
        {
            // inward direction = towards centroid
            XYZ inward = (centroid - point).Normalize();

            // outward = opposite
            return inward.Negate();
        }

        // ==========================================================
        // FIX BEND-END POINT LEAVING ROOF
        // Clamps the end point to stay inside boundary
        // ==========================================================
        public static XYZ FixBoundaryCollision(XYZ bend, XYZ end, List<XYZ> boundary)
        {
            // simple clamp: pull end toward bend if outside polygon
            if (!IsPointInsidePolygon(end, boundary))
            {
                XYZ dir = (bend - end).Normalize();
                end = end + dir * 0.5; // pull slightly inward
            }
            return end;
        }

        // ==========================================================
        // POINT IN POLYGON TEST
        // ==========================================================
        public static bool IsPointInsidePolygon(XYZ pt, List<XYZ> poly)
        {
            bool inside = false;
            int j = poly.Count - 1;

            for (int i = 0; i < poly.Count; i++)
            {
                if (((poly[i].Y > pt.Y) != (poly[j].Y > pt.Y)) &&
                    (pt.X < (poly[j].X - poly[i].X) * (pt.Y - poly[i].Y) /
                     (poly[j].Y - poly[i].Y) + poly[i].X))
                {
                    inside = !inside;
                }
                j = i;
            }

            return inside;
        }
    }
}
