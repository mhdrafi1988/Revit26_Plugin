using Autodesk.Revit.DB;
using Revit26_Plugin.SheetViewArrange.V001.Core.Layout;
using Revit26_Plugin.SheetViewArrange.V001.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.SheetViewArrange.V001.Core.Services
{
    /// <summary>
    /// Reads a sheet's viewports, title block frame and schedules into a <see cref="SheetSnapshot"/>.
    /// Read-only — no transaction needed.
    /// </summary>
    public static class SheetReader
    {
        /// <summary>Reads <paramref name="sheet"/>. Viewports whose geometry cannot be read are left out.</summary>
        public static SheetSnapshot Read(Document doc, ViewSheet sheet)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (sheet == null) throw new ArgumentNullException(nameof(sheet));

            var (frame, frameProblem) = ReadTitleBlockFrame(doc, sheet);

            var viewports = new List<ViewportSnapshot>();
            int unreadable = 0;
            foreach (ElementId id in sheet.GetAllViewports())
            {
                LayoutRect? footprint = null;
                if (doc.GetElement(id) is Viewport vp && doc.GetElement(vp.ViewId) is View view
                    && (footprint = ReadFootprint(vp)) != null)
                {
                    viewports.Add(new ViewportSnapshot
                    {
                        ViewportId = vp.Id,
                        ViewName = view.Name,
                        ViewTypeName = ViewTypeLabel(view.ViewType),
                        DetailNumber = vp.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER)?.AsString()?.Trim() ?? "",
                        Footprint = footprint.Value,
                        IsPinned = vp.Pinned,
                        LockReason = LockReason(doc, vp.Id)
                    });
                }
                else
                {
                    unreadable++;
                }
            }

            return new SheetSnapshot
            {
                SheetId = sheet.Id,
                SheetLabel = $"{sheet.SheetNumber} - {sheet.Name}",
                TitleBlockFrame = frame,
                FrameProblem = frameProblem,
                Viewports = viewports,
                UnreadableCount = unreadable,
                ScheduleCount = new FilteredElementCollector(doc, sheet.Id)
                    .OfClass(typeof(ScheduleSheetInstance))
                    .Cast<ScheduleSheetInstance>()
                    .Count(s => !s.IsTitleblockRevisionSchedule)
            };
        }

        /// <summary>
        /// The viewport box united with its title (label) outline, so titles are aligned and
        /// never overlapped. Null when the box cannot be read.
        /// </summary>
        private static LayoutRect? ReadFootprint(Viewport vp)
        {
            Outline box;
            try
            {
                box = vp.GetBoxOutline();
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                return null;
            }
            if (box == null || box.IsEmpty)
                return null;

            double minX = box.MinimumPoint.X, minY = box.MinimumPoint.Y;
            double maxX = box.MaximumPoint.X, maxY = box.MaximumPoint.Y;

            try
            {
                // Empty / degenerate when the viewport type hides its title.
                Outline label = vp.GetLabelOutline();
                if (label != null && !label.IsEmpty
                    && label.MaximumPoint.X > label.MinimumPoint.X
                    && label.MaximumPoint.Y > label.MinimumPoint.Y)
                {
                    minX = Math.Min(minX, label.MinimumPoint.X);
                    minY = Math.Min(minY, label.MinimumPoint.Y);
                    maxX = Math.Max(maxX, label.MaximumPoint.X);
                    maxY = Math.Max(maxY, label.MaximumPoint.Y);
                }
            }
            catch (Autodesk.Revit.Exceptions.ApplicationException)
            {
                // No readable title — use the box alone.
            }

            var rect = new LayoutRect(minX, minY, maxX, maxY);
            return rect.IsValid ? rect : null;
        }

        private static (LayoutRect? Frame, string Problem) ReadTitleBlockFrame(Document doc, ViewSheet sheet)
        {
            var titleBlocks = new FilteredElementCollector(doc, sheet.Id)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsNotElementType()
                .ToElements();

            if (titleBlocks.Count == 0)
                return (null, "No title block on this sheet — the usable area is measured from the title block.");
            if (titleBlocks.Count > 1)
                return (null, $"This sheet has {titleBlocks.Count} title blocks — the usable area is ambiguous, so nothing is arranged.");

            BoundingBoxXYZ bb = titleBlocks[0].get_BoundingBox(sheet);
            if (bb == null)
                return (null, "The title block's extent could not be read.");

            var rect = new LayoutRect(bb.Min.X, bb.Min.Y, bb.Max.X, bb.Max.Y);
            return rect.IsValid ? (rect, null) : (null, "The title block has no usable extent.");
        }

        /// <summary>
        /// Worksharing / ACC: a viewport owned by someone else, or changed in central since the
        /// last reload, cannot be moved — Revit would refuse the edit at commit time.
        /// </summary>
        private static string LockReason(Document doc, ElementId id)
        {
            if (!doc.IsWorkshared)
                return null;

            CheckoutStatus status = WorksharingUtils.GetCheckoutStatus(doc, id, out string owner);
            if (status == CheckoutStatus.OwnedByOtherUser)
                return "Owned by " + (string.IsNullOrWhiteSpace(owner) ? "another user" : owner);

            if (WorksharingUtils.GetModelUpdatesStatus(doc, id) == ModelUpdatesStatus.UpdatedInCentral)
                return "Changed in central — Reload Latest";

            return null;
        }

        private static string ViewTypeLabel(ViewType type) => type switch
        {
            ViewType.FloorPlan => "Floor Plan",
            ViewType.CeilingPlan => "Ceiling Plan",
            ViewType.EngineeringPlan => "Structural Plan",
            ViewType.AreaPlan => "Area Plan",
            ViewType.Section => "Section",
            ViewType.Elevation => "Elevation",
            ViewType.Detail => "Detail",
            ViewType.DraftingView => "Drafting",
            ViewType.ThreeD => "3D",
            ViewType.Legend => "Legend",
            ViewType.Rendering => "Rendering",
            _ => type.ToString()
        };
    }
}
