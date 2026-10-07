namespace Revit26_Plugin.DwgToDetailLines.V014.Core.Models
{
    /// <summary>
    /// How CAD layers are mapped to Revit line styles or filled region types
    /// (ADDED in V014). Lines and hatches each have their own mode.
    /// </summary>
    public enum LineStyleMappingMode
    {
        /// <summary>
        /// V013 behaviour: each layer uses the style / type with the same name;
        /// a missing one is created or skipped through a per-layer prompt.
        /// </summary>
        LayerName,

        /// <summary>
        /// Each layer uses one entry from a user-picked shortlist of existing
        /// styles / types, auto-assigned by name then appearance and editable
        /// per row. Nothing is created and no prompts are shown.
        /// </summary>
        Shortlist
    }
}
