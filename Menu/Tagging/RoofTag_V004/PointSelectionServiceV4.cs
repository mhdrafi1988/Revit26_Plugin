using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;
using Revit22_Plugin.RoofTagV4.Models;

namespace Revit22_Plugin.RoofTagV4.Services
{
    public static class PointSelectionServiceV4
    {
        /// <summary>
        /// Applies all V4 filtering rules:
        /// ✔ Outer corners (safe-tolerance)
        /// ✔ Dominant point per opening
        /// ✔ Highest-Z point per drain cluster
        /// ✔ Global dedupe
        /// </summary>
        public static List<TagPoint> GetFinalTagPoints(
            RoofBase roof,
            List<XYZ> allVertices,
            List<XYZ> boundary,
            double clutterThresholdMm,
            double drainThresholdMm)
        {
            List<TagPoint> final = new List<TagPoint>();

            if (allVertices == null || allVertices.Count == 0)
                return final;

            double clutterFt = clutterThresholdMm / 304.8;
            double drainFt = drainThresholdMm / 304.8;

            // -----------------------------------------------
            // 1️⃣ OUTER CORNER POINTS (PATCHED TOLERANCE 1.0 ft)
            // -----------------------------------------------
            List<XYZ> outer = ExtractOuterCornerPoints(allVertices, boundary);

            List<XYZ> filteredOuter = FilterByClutter(outer, clutterFt);

            foreach (XYZ p in filteredOuter)
                final.Add(new TagPoint(p, TagPointType.Corner));

            // -----------------------------------------------
            // 2️⃣ INNER OPENINGS → dominant point only
            // -----------------------------------------------
            List<XYZ> inner = allVertices.Except(outer).ToList();

            List<List<XYZ>> openingGroups = GroupOpeningPoints(inner);

            foreach (var group in openingGroups)
            {
                if (group.Count == 0) continue;

                XYZ best = PickDominantOpeningPoint(group);
                final.Add(new TagPoint(best, TagPointType.InnerOpening));
            }

            // -----------------------------------------------
            // 3️⃣ DRAIN GROUPS → highest Z only
            // -----------------------------------------------
            List<List<XYZ>> drainGroups = GroupDrainPoints(allVertices, drainFt);

            foreach (var group in drainGroups)
            {
                if (group.Count == 0) continue;

                XYZ highest = group.OrderByDescending(p => p.Z).First();
                final.Add(new TagPoint(highest, TagPointType.Drain));
            }

            // -----------------------------------------------
            // 4️⃣ FINAL GLOBAL DE-DUPLICATION
            // -----------------------------------------------
            final = RemoveDuplicateTagPoints(final, 0.5); // 0.5 ft ≈ 150 mm

            return final;
        }



        // =====================================================
        // OUTER CORNER EXTRACTION (PATCHED TOLERANCE)
        // =====================================================
        private static List<XYZ> ExtractOuterCornerPoints(
            List<XYZ> allPts,
            List<XYZ> boundary)
        {
            if (boundary == null || boundary.Count == 0)
                return allPts;

            double tolFt = 1.0; // ★ patched from 0.5 → 1.0 ft (304 mm)

            return allPts
                .Where(p => boundary.Any(b => b.DistanceTo(p) < tolFt))
                .ToList();
        }



        // =====================================================
        // CLUTTER FILTERING
        // =====================================================
        private static List<XYZ> FilterByClutter(List<XYZ> pts, double threshold)
        {
            List<XYZ> output = new List<XYZ>();

            foreach (var p in pts)
            {
                if (!output.Any(x => x.DistanceTo(p) < threshold))
                    output.Add(p);
            }

            return output;
        }



        // =====================================================
        // GROUP INNER OPENING POINTS
        // =====================================================
        private static List<List<XYZ>> GroupOpeningPoints(List<XYZ> pts)
        {
            List<List<XYZ>> groups = new List<List<XYZ>>();
            if (pts.Count == 0) return groups;

            XYZ centroid = new XYZ(
                pts.Average(p => p.X),
                pts.Average(p => p.Y),
                pts.Average(p => p.Z));

            List<XYZ> sorted = pts
                .OrderBy(p => p.DistanceTo(centroid))
                .ToList();

            List<XYZ> current = new List<XYZ>();
            double gap = 2.0; // 2 ft ≈ 600 mm

            for (int i = 0; i < sorted.Count; i++)
            {
                if (i == 0 || sorted[i].DistanceTo(sorted[i - 1]) < gap)
                    current.Add(sorted[i]);
                else
                {
                    groups.Add(new List<XYZ>(current));
                    current.Clear();
                    current.Add(sorted[i]);
                }
            }

            if (current.Count > 0)
                groups.Add(current);

            return groups;
        }



        // =====================================================
        // PICK DOMINANT OPENING POINT
        // =====================================================
        private static XYZ PickDominantOpeningPoint(List<XYZ> pts)
        {
            if (pts.Count == 1) return pts[0];

            XYZ centroid = new XYZ(
                pts.Average(p => p.X),
                pts.Average(p => p.Y),
                pts.Average(p => p.Z));

            // Choose point:
            // 1) Highest elevation
            // 2) Farthest from centroid (stronger geometric point)
            return pts
                .OrderByDescending(p => p.Z)
                .ThenByDescending(p => p.DistanceTo(centroid))
                .First();
        }



        // =====================================================
        // GROUP DRAIN POINTS BY DISTANCE
        // =====================================================
        private static List<List<XYZ>> GroupDrainPoints(List<XYZ> pts, double threshold)
        {
            List<List<XYZ>> groups = new List<List<XYZ>>();
            HashSet<XYZ> used = new HashSet<XYZ>(new XYZComparer());

            foreach (var p in pts)
            {
                if (used.Contains(p)) continue;

                var cluster = pts
                    .Where(x => x.DistanceTo(p) < threshold)
                    .ToList();

                foreach (var c in cluster)
                    used.Add(c);

                groups.Add(cluster);
            }

            return groups;
        }



        // =====================================================
        // GLOBAL DE-DUPLICATION
        // =====================================================
        private static List<TagPoint> RemoveDuplicateTagPoints(
            List<TagPoint> pts,
            double toleranceFt)
        {
            List<TagPoint> clean = new List<TagPoint>();

            foreach (var p in pts)
            {
                if (!clean.Any(c => c.Point.DistanceTo(p.Point) < toleranceFt))
                    clean.Add(p);
            }

            return clean;
        }



        // =====================================================
        // COMPARER FOR XYZ HASHING
        // =====================================================
        class XYZComparer : IEqualityComparer<XYZ>
        {
            public bool Equals(XYZ a, XYZ b)
            {
                return a.IsAlmostEqualTo(b);
            }

            public int GetHashCode(XYZ p)
            {
                return p.X.GetHashCode() ^ p.Y.GetHashCode() ^ p.Z.GetHashCode();
            }
        }
    }
}
