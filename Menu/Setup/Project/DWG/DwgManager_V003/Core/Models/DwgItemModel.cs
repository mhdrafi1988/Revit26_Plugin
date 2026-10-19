using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin_Revised.DwgManager.V003.Core.Models
{
    /// <summary>
    /// One row of the DWG Manager list: every instance of one CAD file (link or import) that is placed
    /// in one view. A file placed in three views gives three rows; model-level instances (visible in all
    /// views) share one row with no owner view.
    /// </summary>
    public sealed class DwgItemModel
    {
        /// <summary>The CAD link or import type (one type per CAD file).</summary>
        public ElementId TypeId { get; init; }

        /// <summary>View that owns the instances, or <see cref="ElementId.InvalidElementId"/> for model-level instances.</summary>
        public ElementId OwnerViewId { get; init; }

        /// <summary>CAD file name as Revit shows it, with its real extension (.dwg, .dxf, .dgn, …).</summary>
        public string FileName { get; init; }

        /// <summary>Full path of a linked file, or empty for imports and unresolved links.</summary>
        public string FilePath { get; init; }

        /// <summary>Name of the owner view, or "(all views)" for model-level instances.</summary>
        public string ViewName { get; init; }

        /// <summary>View type label used for the second grouping level (Floor Plans, Sections, …).</summary>
        public string ViewCategory { get; init; }

        /// <summary>True for a CAD link, false for an imported CAD file.</summary>
        public bool IsLinked { get; init; }

        /// <summary>The instances of <see cref="TypeId"/> owned by <see cref="OwnerViewId"/>.</summary>
        public List<ElementId> InstanceIds { get; init; } = new List<ElementId>();
    }
}
