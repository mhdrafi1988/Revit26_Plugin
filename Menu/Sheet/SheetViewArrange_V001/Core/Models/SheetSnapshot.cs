using Autodesk.Revit.DB;
using Revit26_Plugin.SheetViewArrange.V001.Core.Layout;
using System.Collections.Generic;

namespace Revit26_Plugin.SheetViewArrange.V001.Core.Models
{
    /// <summary>
    /// One viewport on the sheet, read once so layout and preview never touch the Revit API.
    /// The footprint is the union of the viewport box and its title (label), in sheet feet.
    /// </summary>
    public sealed class ViewportSnapshot
    {
        /// <summary>The viewport element.</summary>
        public ElementId ViewportId { get; init; }

        /// <summary>The viewport's element id value — identifies it across refreshes (0 when there is no id).</summary>
        public long Key => ViewportId?.Value ?? 0;

        /// <summary>Name of the view shown in the viewport.</summary>
        public string ViewName { get; init; }

        /// <summary>Display name of the view type (Floor Plan, Section, ...).</summary>
        public string ViewTypeName { get; init; }

        /// <summary>The viewport's Detail Number; empty when it has none.</summary>
        public string DetailNumber { get; init; }

        /// <summary>Footprint (view box ∪ title) in sheet feet.</summary>
        public LayoutRect Footprint { get; init; }

        /// <summary>True when the viewport is pinned.</summary>
        public bool IsPinned { get; init; }

        /// <summary>
        /// Why the viewport cannot be edited right now (owned by another user, or changed in
        /// central and needing Reload Latest), or null when it can be moved.
        /// </summary>
        public string LockReason { get; init; }
    }

    /// <summary>Everything the tool needs to know about one sheet, read in a single pass.</summary>
    public sealed class SheetSnapshot
    {
        /// <summary>The sheet.</summary>
        public ElementId SheetId { get; init; }

        /// <summary>"A101 - Sheet Name".</summary>
        public string SheetLabel { get; init; }

        /// <summary>Title block bounding box in sheet feet, or null when it could not be used.</summary>
        public LayoutRect? TitleBlockFrame { get; init; }

        /// <summary>Why <see cref="TitleBlockFrame"/> is null (no / several title blocks, unreadable geometry).</summary>
        public string FrameProblem { get; init; }

        /// <summary>Viewports on the sheet, unsorted.</summary>
        public List<ViewportSnapshot> Viewports { get; init; } = new();

        /// <summary>Schedules placed on the sheet. They are not viewports and are not moved.</summary>
        public int ScheduleCount { get; init; }

        /// <summary>Viewports whose outline could not be read; they are left out and not moved.</summary>
        public int UnreadableCount { get; init; }
    }
}
