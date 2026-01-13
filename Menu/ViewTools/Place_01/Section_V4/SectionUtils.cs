using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.SectionPlacement
{
    public static class SectionUtils
    {
        /// <summary>
        /// Collects all section views visible in the active plan (placed + unplaced).
        /// </summary>
        public static List<ViewSection> CollectAllSectionsInPlan(Document doc, View activeView)
        {
            var sections = new FilteredElementCollector(doc, activeView.Id)
                                .OfClass(typeof(ViewSection))
                                .Cast<ViewSection>()
                                .ToList();

            return sections;
        }

        /// <summary>
        /// Main move logic: removes existing viewports, places sections in target sheets using grid, avoids collisions, creates new sheets if needed.
        /// </summary>
        public static void MoveSectionsToSheets(Document doc, List<ViewSection> sections, SectionPlacementViewModel vm)
        {
            int movedCount = 0;
            int skippedCount = 0;
            int newSheetsCount = 0;

            int sectionIndex = 0;
            int capacity = vm.Rows * vm.Columns;

            // Start with selected sheets
            List<ViewSheet> targetSheets = vm.SelectedSheets.ToList();

            // If none selected, stop
            if (targetSheets.Count == 0)
            {
                TaskDialog.Show("Error", "No target sheets selected.");
                return;
            }

            foreach (var sheet in targetSheets)
            {
                sectionIndex = PlaceOnOneSheet(doc, sheet, sections, sectionIndex, vm, ref movedCount, ref skippedCount);
                if (sectionIndex >= sections.Count) break;
            }

            // If overflow remains
            if (sectionIndex < sections.Count && vm.AutoCreateNewSheets)
            {
                while (sectionIndex < sections.Count)
                {
                    ViewSheet newSheet = ViewSheet.Create(doc, vm.SelectedTitleBlock.Id);
                    newSheetsCount++;
                    sectionIndex = PlaceOnOneSheet(doc, newSheet, sections, sectionIndex, vm, ref movedCount, ref skippedCount);
                }
            }

            // Final summary
            TaskDialog.Show("Summary",
                $"✅ Sections moved: {movedCount}\n" +
                $"⚠️ Skipped: {skippedCount}\n" +
                $"📄 New sheets created: {newSheetsCount}");
        }

        /// <summary>
        /// Places as many sections as possible on one sheet, respecting grid + avoiding collisions.
        /// </summary>
        private static int PlaceOnOneSheet(Document doc, ViewSheet sheet, List<ViewSection> sections, int sectionIndex,
                                           SectionPlacementViewModel vm, ref int movedCount, ref int skippedCount)
        {
            // Collect existing viewport bounding boxes on this sheet
            var existingViewports = new FilteredElementCollector(doc, sheet.Id)
                                        .OfClass(typeof(Viewport))
                                        .Cast<Viewport>()
                                        .ToList();

            var occupiedBoxes = existingViewports
                .Select(vp => vp.GetBoxOutline().ToBoundingBoxXYZ())
                .ToList();

            for (int r = 0; r < vm.Rows; r++)
            {
                for (int c = 0; c < vm.Columns; c++)
                {
                    if (sectionIndex >= sections.Count) return sectionIndex;

                    ViewSection section = sections[sectionIndex];

                    // Delete old viewport if placed
                    DeleteViewportIfExists(doc, section);

                    // Assign template if chosen
                    if (vm.SelectedViewTemplate != null)
                        section.ViewTemplateId = vm.SelectedViewTemplate.Id;

                    // Compute placement point
                    XYZ location = new XYZ(c * vm.XGap, -r * vm.YGap, 0);

                    // Create a temporary viewport to check collision
                    try
                    {
                        Viewport vp = Viewport.Create(doc, sheet.Id, section.Id, location);
                        BoundingBoxXYZ bb = vp.GetBoxOutline().ToBoundingBoxXYZ();

                        if (occupiedBoxes.Any(box => BoxesOverlap(box, bb)))
                        {
                            // Overlap detected → remove and skip this slot
                            doc.Delete(vp.Id);
                            skippedCount++;
                        }
                        else
                        {
                            // Accept placement
                            occupiedBoxes.Add(bb);
                            movedCount++;
                            sectionIndex++;
                        }
                    }
                    catch
                    {
                        skippedCount++;
                        sectionIndex++;
                    }
                }
            }

            return sectionIndex;
        }

        /// <summary>
        /// Removes viewport if section is already placed.
        /// </summary>
        private static void DeleteViewportIfExists(Document doc, ViewSection section)
        {
            var vp = new FilteredElementCollector(doc)
                        .OfClass(typeof(Viewport))
                        .Cast<Viewport>()
                        .FirstOrDefault(v => v.ViewId == section.Id);
            if (vp != null)
            {
                doc.Delete(vp.Id);
            }
        }

        /// <summary>
        /// Checks if two bounding boxes overlap.
        /// </summary>
        private static bool BoxesOverlap(BoundingBoxXYZ a, BoundingBoxXYZ b)
        {
            return (a.Min.X < b.Max.X && a.Max.X > b.Min.X) &&
                   (a.Min.Y < b.Max.Y && a.Max.Y > b.Min.Y);
        }

        /// <summary>
        /// Helper: Convert Outline to BoundingBoxXYZ.
        /// </summary>
        private static BoundingBoxXYZ ToBoundingBoxXYZ(this Outline outline)
        {
            return new BoundingBoxXYZ
            {
                Min = outline.MinimumPoint,
                Max = outline.MaximumPoint
            };
        }
    }
}
