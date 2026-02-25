using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.RoofTag_V90
{
    /// <summary>
    /// Core geometry helpers used by RoofTag V3.
    /// Contains boundary, centroid, projection, bend, and collision logic.
    /// </summary>
    public static partial class GeometryHelperV3
    {
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
                if (obj is not Solid solid || solid.Faces.Size == 0)
                    continue;

                foreach (Face f in solid.Faces)
                {
                    if (f is not PlanarFace pf)
                        continue;

                    if (pf.FaceNormal.Z < 0.7)
                        continue;

                    EdgeArrayArray loops = pf.EdgeLoops;
                    if (loops.Size == 0)
                        continue;

                    EdgeArray ea = loops.get_Item(0);
                    foreach (Edge e in ea)
                    {
                        foreach (XYZ p in e.Tessellate())
                            boundary.Add(new XYZ(p.X, p.Y, 0));
                    }
                }
            }

            return boundary;
        }

        // ================================================================
        // 4. PROJECT TO FACE FOR TAGGING
        // ================================================================
        public static void GetTaggingReferenceOnRoof(
            RoofBase roof,
            XYZ inputPt,
            out XYZ projected,
            out Reference faceRef)
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
                if (obj is not Solid solid || solid.Faces.Size == 0)
                    continue;

                foreach (Face face in solid.Faces)
                {
                    if (face is not PlanarFace pf)
                        continue;

                    if (pf.FaceNormal.Z < 0.2)
                        continue;

                    IntersectionResult proj = pf.Project(inputPt);
                    if (proj == null)
                        continue;

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
        public static XYZ ComputeBendPoint(
            XYZ origin,
            XYZ centroid,
            double offsetFt,
            bool inward)
        {
            XYZ dir = inward ? (centroid - origin) : (origin - centroid);
            if (dir.GetLength() < 1e-6)
                return origin;

            return origin + dir.Normalize() * offsetFt;
        }

        // ================================================================
        // 6. OUTWARD DIRECTION FROM ROOF BOUNDARY
        // ================================================================
        public static XYZ GetOutwardDirectionForPoint(
            XYZ pt,
            List<XYZ> boundary,
            XYZ centroid)
        {
            if (boundary == null || boundary.Count < 3)
                return (pt - centroid).Normalize();

            XYZ closest = boundary.OrderBy(b => b.DistanceTo(pt)).First();
            XYZ dir = closest - centroid;

            return dir.GetLength() < 1e-6
                ? (pt - centroid).Normalize()
                : dir.Normalize();
        }

        // ================================================================
        // 7. END POINT WITH ANGLE
        // ================================================================
        public static XYZ ComputeEndPointWithAngle(
            XYZ origin,
            XYZ bend,
            double angleDeg,
            double offsetFt,
            XYZ outwardDir,
            bool bendInward)
        {
            int xSign = bendInward
                ? (bend.X >= origin.X ? 1 : -1)
                : (outwardDir.X >= 0 ? 1 : -1);

            double angleRad = angleDeg * Math.PI / 180.0;

            XYZ angledDir = new XYZ(
                Math.Cos(angleRad) * xSign,
                Math.Sin(angleRad),
                0).Normalize();

            double half = offsetFt * 0.5;

            XYZ angledPoint = bend + angledDir * half;
            XYZ horizontal = new XYZ(xSign, 0, 0);

            return angledPoint + horizontal * half;
        }

        // ================================================================
        // 8. COLLISION FIX
        // ================================================================
        public static XYZ AdjustForBoundaryCollisions(
            XYZ bend,
            XYZ end,
            List<XYZ> boundary)
        {
            if (boundary == null || boundary.Count < 2)
                return end;

            if (!LineIntersectsPolygon(bend, end, boundary))
                return end;

            XYZ flipped = bend - (end - bend);

            return LineIntersectsPolygon(bend, flipped, boundary)
                ? end
                : flipped;
        }

        // --- helpers omitted for brevity ---
    }
}
