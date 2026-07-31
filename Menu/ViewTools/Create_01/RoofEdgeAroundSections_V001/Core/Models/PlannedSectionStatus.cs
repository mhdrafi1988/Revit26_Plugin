namespace Revit26_Plugin.RoofEdgeSections.V001
{
    /// <summary>
    /// Status of a single planned section row in the preview table,
    /// determined during the plan-build pass (before Run).
    /// </summary>
    public enum PlannedSectionStatus
    {
        /// <summary>Edge found, no name collision — ready to create.</summary>
        Ready,

        /// <summary>A view with the same name already exists — will be skipped, not overwritten.</summary>
        AlreadyExists,

        /// <summary>No boundary edge found close/parallel enough to this bounding-box side.</summary>
        NoEdgeFound
    }
}
