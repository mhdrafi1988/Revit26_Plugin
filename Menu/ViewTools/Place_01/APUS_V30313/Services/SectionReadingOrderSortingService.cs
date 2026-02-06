using Autodesk.Revit.DB;
using Revit26_Plugin.APUS_V313.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.APUS_V313.Services
{
    /// <summary>
    /// Sorts sections in human reading order: 
    /// 1. Group into rows by Y tolerance (TOP ? BOTTOM)
    /// 2. Sort each row LEFT ? RIGHT
    /// 3. Returns final list order (NEVER change again)
    /// </summary>
    public static class SectionReadingOrderSortingService
    {
        public static IList<SectionItemViewModel> SortInReadingOrder(
            IList<SectionItemViewModel> sections,
            double rowToleranceMm)
        {
            if (sections == null || sections.Count == 0)
                return sections;

            // Convert tolerance to internal units
            double rowTolerance = UnitUtils.ConvertToInternalUnits(
                rowToleranceMm,
                UnitTypeId.Millimeters);

            // -----------------------------------------------------------------
            // STEP 1: Calculate anchor points for all sections
            // -----------------------------------------------------------------
            var sectionsWithAnchors = sections.Select(s => new
            {
                Section = s,
                Anchor = CalculateAnchorPoint(s.View)
            }).ToList();

            // -----------------------------------------------------------------
            // STEP 2: Group into rows by Y coordinate using tolerance
            // -----------------------------------------------------------------
            var rows = new List<List<(SectionItemViewModel Section, XYZ Anchor)>>();
            var currentRow = new List<(SectionItemViewModel Section, XYZ Anchor)>();

            // Pre-sort by Y (TOP ? BOTTOM = descending Y)
            var sortedByY = sectionsWithAnchors
                .OrderByDescending(x => x.Anchor.Y)
                .ThenBy(x => x.Anchor.X)
                .ToList();

            foreach (var item in sortedByY)
            {
                if (currentRow.Count == 0)
                {
                    currentRow.Add((item.Section, item.Anchor));
                    continue;
                }

                double referenceY = currentRow[0].Anchor.Y;
                double currentY = item.Anchor.Y;

                if (Math.Abs(currentY - referenceY) <= rowTolerance)
                {
                    // Same row
                    currentRow.Add((item.Section, item.Anchor));
                }
                else
                {
                    // New row
                    rows.Add(currentRow);
                    currentRow = new List<(SectionItemViewModel Section, XYZ Anchor)>
                    {
                        (item.Section, item.Anchor)
                    };
                }
            }

            if (currentRow.Count > 0)
                rows.Add(currentRow);

            // -----------------------------------------------------------------
            // STEP 3: Sort each row LEFT ? RIGHT
            // -----------------------------------------------------------------
            foreach (var row in rows)
            {
                row.Sort((a, b) => a.Anchor.X.CompareTo(b.Anchor.X));
            }

            // -----------------------------------------------------------------
            // STEP 4: Flatten rows TOP ? BOTTOM, LEFT ? RIGHT
            // -----------------------------------------------------------------
            return rows
                .SelectMany(row => row.Select(item => item.Section))
                .ToList();
        }

        /// <summary>
        /// Calculates anchor point as midpoint of section's bounding box (X,Y only)
        /// </summary>
        private static XYZ CalculateAnchorPoint(ViewSection section)
        {
            if (section == null)
                return XYZ.Zero;

            try
            {
                BoundingBoxXYZ bb = section.CropBox;
                if (bb == null)
                    return XYZ.Zero;

                // Calculate midpoint of bounding box
                XYZ min = bb.Min;
                XYZ max = bb.Max;

                return new XYZ(
                    (min.X + max.X) * 0.5,
                    (min.Y + max.Y) * 0.5,
                    0); // Z is ignored
            }
            catch
            {
                return XYZ.Zero;
            }
        }
    }
}