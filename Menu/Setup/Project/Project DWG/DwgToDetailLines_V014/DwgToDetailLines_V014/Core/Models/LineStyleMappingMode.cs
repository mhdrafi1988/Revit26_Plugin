namespace Revit26_Plugin.DwgToDetailLines.V014.Core.Models
{
    /// <summary>
    /// How CAD line layers are mapped to Revit line styles (ADDED in V014).
    /// </summary>
    public enum LineStyleMappingMode
    {
        /// <summary>
        /// V013 behaviour: each layer uses the line style with the same name;
        /// a missing one is created or skipped through a per-layer prompt.
        /// </summary>
        LayerName,

        /// <summary>
        /// Each layer uses one style from a user-picked shortlist of existing
        /// line styles, auto-assigned by CAD colour / lineweight / pattern and
        /// editable per row. No styles are created and no prompts are shown.
        /// </summary>
        Shortlist
    }
}
