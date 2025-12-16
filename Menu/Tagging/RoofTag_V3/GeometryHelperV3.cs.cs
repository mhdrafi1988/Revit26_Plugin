using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.RoofTagV3
{
    public static class GeometryHelperV3
    {
        // ================================================================
        // 1. GET EXACT SHAPE EDITOR VERTICES
        // ================================================================
        public static List<XYZ> GetExactShapeVertices(RoofBase roof)
        {
            List<XYZ> pts = new List<XYZ>();

            // Use the GetSlabShapeEditor() method instead of the missing SlabShapeEditor property
            var shapeEditor = roof.GetSlabShapeEditor();
            if (shapeEditor == null || !shapeEditor.IsEnabled)
                return pts;

            foreach (SlabShapeVertex v in shapeEditor.SlabShapeVertices)
                pts.Add(v.Position);

            return pts;
        }

        // ================================================================
        // 2. CENTROID (XY only)
        // ================================================================
        public static XYZ GetXYCentroid(List<XYZ> pts)
        {
            double x = pts.Average(p => p.X);
            double y = pts.Average(p => p.Y);
            return new XYZ(x, y, 0);
        }

        // ================================================================
        // 3. BUILD ROOF TOP BOUNDARY (XY polygon)
        // ================================================================
        public static List<XYZ> BuildRoofBoundaryXY(RoofBase roof)
        {
            List<XYZ> boundary = new List<XYZ>();

            Options opt = new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geom = roof.get_Geometry(opt);
            if (geom == null) return boundary;

            foreach (GeometryObject obj in geom)
            {
                Solid solid = obj as Solid;
                if (solid == null || solid.Faces.Size == 0) continue;

                foreach (Face f in solid.Faces)
                {
                    PlanarFace pf = f as PlanarFace;
                    if (pf == null) continue;

                    if (pf.FaceNormal.Z > 0.7)
                    {
                        EdgeArrayArray loops = pf.EdgeLoops;
                        if (loops.Size > 0)
                        {
                            EdgeArray ea = loops.get_Item(0);
                            foreach (Edge e in ea)
                            {
                                IList<XYZ> pts = e.Tessellate();
                                foreach (XYZ p in pts)
                                    boundary.Add(new XYZ(p.X, p.Y, 0));
                            }
                        }
                    }
                }
            }

            return boundary;
        }

        // ================================================================
        // 4. PROJECT TO FACE FOR TAGGING
        // ================================================================
        public static void GetTaggingReferenceOnRoof(
            RoofBase roof, XYZ inputPt, out XYZ projected, out Reference faceRef)
        {
            projected = null;
            faceRef = null;

            Options opt = new Options
            {
                ComputeReferences = true,
                DetailLevel = ViewDetailLevel.Fine,
                IncludeNonVisibleObjects = false
            };

            GeometryElement geom = roof.get_Geometry(opt);
            if (geom == null) return;

            double bestDist = double.MaxValue;

            foreach (GeometryObject obj in geom)
            {
                Solid solid = obj as Solid;
                if (solid == null || solid.Faces.Size == 0) continue;

                foreach (Face face in solid.Faces)
                {
                    PlanarFace pf = face as PlanarFace;
                    if (pf == null) continue;
                    if (pf.FaceNormal.Z < 0.2) continue;

                    IntersectionResult proj = pf.Project(inputPt);
                    if (proj == null) continue;

                    double d = inputPt.DistanceTo(proj.XYZPoint);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        projected = proj.XYZPoint;
                        faceRef = pf.Reference;
                    }
                }
            }
        }

        // ================================================================
        // 5. BEND POINT
        // ================================================================
        public static XYZ ComputeBendPoint(XYZ origin, XYZ centroid, double offsetFt, bool inward)
        {
            XYZ dir = inward ? (centroid - origin) : (origin - centroid);

            if (dir.IsZeroLength()) return origin;

            return origin + dir.Normalize() * offsetFt;
        }

        // ================================================================
        // 6. OUTWARD DIRECTION FROM ROOF BOUNDARY
        // ================================================================
        public static XYZ GetOutwardDirectionForPoint(XYZ pt, List<XYZ> boundary, XYZ centroid)
        {
            if (boundary == null || boundary.Count < 3)
                return (pt - centroid).Normalize();

            XYZ closest = boundary.OrderBy(b => b.DistanceTo(pt)).FirstOrDefault();

            XYZ outward = (pt - centroid).Normalize();
            if (closest != null)
            {
                XYZ normal = closest - centroid;
                if (!normal.IsZeroLength())
                    outward = normal.Normalize();
            }

            return outward;
        }

        // ================================================================
        // 7. OVERLOAD 1 (COMPATIBILITY)
        // ================================================================
        public static XYZ ComputeEndPointWithAngle(
            XYZ origin,
            XYZ bend,
            double chosenAngleDeg,
            double offsetFt,
            XYZ outwardDir)
        {
            return ComputeEndPointWithAngle(
                origin,
                bend,
                chosenAngleDeg,
                offsetFt,
                outwardDir,
                false);
        }

        // ================================================================
        // 7b. STRICT MODE (TEXT FOLLOWS BEND SIDE)
        // ================================================================
        public static XYZ ComputeEndPointWithAngle(
            XYZ origin,
            XYZ bend,
            double chosenAngleDeg,
            double offsetFt,
            XYZ outwardDir,
            bool bendInward)
        {
            int xSign;

            if (bendInward)
            {
                xSign = (bend.X >= origin.X) ? 1 : -1;
            }
            else
            {
                xSign = (outwardDir.X >= 0) ? 1 : -1;
            }

            double angleRad = chosenAngleDeg * Math.PI / 180.0;

            double ax = Math.Cos(angleRad) * xSign;
            double ay = Math.Sin(angleRad);

            XYZ angledDir = new XYZ(ax, ay, 0).Normalize();

            double half = offsetFt * 0.5;

            XYZ angledPoint = bend + angledDir * half;

            XYZ horizontalDir = new XYZ(xSign, 0, 0);

            XYZ finalPoint = angledPoint + horizontalDir * half;

            return finalPoint;
        }

        // ================================================================
        // 8. COLLISION FIX (unchanged)
        // ================================================================
        public static XYZ AdjustForBoundaryCollisions(
            XYZ bend, XYZ end, List<XYZ> boundary)
        {
            if (boundary == null || boundary.Count < 2)
                return end;

            if (LineIntersectsPolygon(bend, end, boundary))
            {
                XYZ flipped = bend - (end - bend);

                if (LineIntersectsPolygon(bend, flipped, boundary))
                {
                    XYZ extended = bend + (flipped - bend).Normalize() * 3.0;

                    if (!LineIntersectsPolygon(bend, extended, boundary))
                        return extended;

                    return end;
                }

                return flipped;
            }

            return end;
        }

        private static bool LineIntersectsPolygon(XYZ a, XYZ b, List<XYZ> poly)
        {
            for (int i = 0; i < poly.Count; i++)
            {
                XYZ p1 = poly[i];
                XYZ p2 = poly[(i + 1) % poly.Count];

                if (SegmentsIntersect2D(a, b, p1, p2))
                    return true;
            }
            return false;
        }

        private static bool SegmentsIntersect2D(XYZ p1, XYZ p2, XYZ p3, XYZ p4)
        {
            return DoIntersect(To2D(p1), To2D(p2), To2D(p3), To2D(p4));
        }

        private static bool DoIntersect(XY a, XY b, XY c, XY d)
        {
            return (Orientation(a, b, c) != Orientation(a, b, d)) &&
                   (Orientation(c, d, a) != Orientation(c, d, b));
        }

        private static int Orientation(XY p, XY q, XY r)
        {
            double val = (q.Y - p.Y) * (r.X - q.X) -
                         (q.X - p.X) * (r.Y - q.Y);

            if (Math.Abs(val) < 1e-9) return 0;
            return (val > 0) ? 1 : 2;
        }

        private struct XY
        {
            public double X, Y;
            public XY(double x, double y) { X = x; Y = y; }
        }

        private static XY To2D(XYZ p)
        {
            return new XY(p.X, p.Y);
        }

        public static bool IsZeroLength(this XYZ v)
        {
            return v.GetLength() < 1e-6;
        }
    }
}
