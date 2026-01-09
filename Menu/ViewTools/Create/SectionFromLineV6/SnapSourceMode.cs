using System;

namespace Revit22_Plugin.PlanSections.Services
{
    /// <summary>
    /// Defines where the section should look for elements (Floors/Roofs)
    /// when determining the vertical Z-range for the bounding box.
    /// </summary>
    public enum SnapSourceMode
    {
        /// <summary>
        /// Only search HOST document elements (Floors / Roofs).
        /// </summary>
        HostOnly = 0,

        /// <summary>
        /// Only search LINKED model elements (Floors / Roofs).
        /// </summary>
        LinkedOnly = 1,

        /// <summary>
        /// Search BOTH host and linked documents.
        /// </summary>
        HostAndLinked = 2
    }
}
