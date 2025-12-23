using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_V08.Commands.Services
{
    /// <summary>
    /// Removes duplicate crease lines.
    /// Supports two modes:
    /// 1) Merge collinear creases
    /// 2) Force one crease per corner (no merging)
    /// </summary>
    public class DuplicateLineRemovalService
    {
        private const double Tol = 1e-6;

        public IList<Line> RemoveDuplicates(
            IList<Line> input,
            bool mergeCollinear,
            out int removedCount)
        {
            removedCount = 0;

            if (input == null || input.Count == 0)
                return new List<Line>();

            if (!mergeCollinear)
            {
                // --------------------------------------------------
                // MODE: Force one crease per corner
                // --------------------------------------------------
                return KeepAll(input, out removedCount);
            }

            // --------------------------------------------------
            // MODE: Merge collinear creases
            // --------------------------------------------------
            return MergeCollinear(input, out removedCount);
        }

        // ======================================================
        // MODE 1 — FORCE ONE CREASE PER CORNER
        // ======================================================
        private static IList<Line> KeepAll(
            IList<Line> input,
            out int removedCount)
        {
            removedCount = 0;
            List<Line> result = new();
            HashSet<string> seen = new();

            foreach (Line line in input)
            {
                XYZ p0 = line.GetEndPoint(0);
                XYZ p1 = line.GetEndPoint(1);

                string key =
                    $"{p0.X:F9},{p0.Y:F9},{p0.Z:F9}|" +
                    $"{p1.X:F9},{p1.Y:F9},{p1.Z:F9}";

                if (seen.Contains(key))
                {
                    removedCount++;
                    continue;
                }

                seen.Add(key);
                result.Add(line);
            }

            return result;
        }

        // ======================================================
        // MODE 2 — MERGE COLLINEAR CREASES
        // ======================================================
        private static IList<Line> MergeCollinear(
            IList<Line> input,
            out int removedCount)
        {
            removedCount = 0;

            List<Line> normalized = new();
            foreach (Line line in input)
                normalized.Add(Normalize(line));

            List<List<Line>> groups = new();

            foreach (Line line in normalized)
            {
                bool added = false;

                foreach (var group in groups)
                {
                    if (AreCollinear(group[0], line))
                    {
                        group.Add(line);
                        added = true;
                        break;
                    }
                }

                if (!added)
                    groups.Add(new List<Line> { line });
            }

            List<Line> result = new();

            foreach (var group in groups)
            {
                if (group.Count == 1)
                {
                    result.Add(group[0]);
                    continue;
                }

                Line merged = MergeGroup(group);
                result.Add(merged);
                removedCount += group.Count - 1;
            }

            return result;
        }

        // ======================================================
        // Geometry helpers
        // ======================================================
        private static Line Normalize(Line line)
        {
            XYZ p0 = line.GetEndPoint(0);
            XYZ p1 = line.GetEndPoint(1);

            if (p1.X < p0.X ||
               (Math.Abs(p1.X - p0.X) < Tol && p1.Y < p0.Y))
            {
                return Line.CreateBound(p1, p0);
            }

            return line;
        }

        private static bool AreCollinear(Line a, Line b)
        {
            XYZ aDir = (a.GetEndPoint(1) - a.GetEndPoint(0)).Normalize();
            XYZ bDir = (b.GetEndPoint(1) - b.GetEndPoint(0)).Normalize();

            if (aDir.CrossProduct(bDir).GetLength() > Tol)
                return false;

            XYZ v = b.GetEndPoint(0) - a.GetEndPoint(0);
            if (v.CrossProduct(aDir).GetLength() > Tol)
                return false;

            return true;
        }

        private static Line MergeGroup(List<Line> lines)
        {
            XYZ dir = (lines[0].GetEndPoint(1) - lines[0].GetEndPoint(0)).Normalize();
            XYZ origin = lines[0].GetEndPoint(0);

            double min = double.PositiveInfinity;
            double max = double.NegativeInfinity;

            foreach (Line line in lines)
            {
                for (int i = 0; i < 2; i++)
                {
                    double t = (line.GetEndPoint(i) - origin).DotProduct(dir);
                    min = Math.Min(min, t);
                    max = Math.Max(max, t);
                }
            }

            return Line.CreateBound(
                origin + min * dir,
                origin + max * dir);
        }
    }
}
