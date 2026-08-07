namespace Revit26_Plugin.CreateSectionsFromDetailLines.V008.Models
{
    /// <summary>
    /// Independent host-category selection flags, bound to the four
    /// "Host Source" checkboxes in the dialog (Floor/Roof x Host/Linked).
    ///
    /// V008: replaces the single SnapSourceMode enum from V07's
    /// (HostOnly / LinkedOnly / HostAndLinked) dropdown. Any combination
    /// of the four categories can now be searched independently.
    /// All default to true (checked) per approved mockup.
    /// </summary>
    public class HostSourceSelection
    {
        public bool FloorHost { get; init; } = true;
        public bool RoofHost { get; init; } = true;
        public bool FloorLinked { get; init; } = true;
        public bool RoofLinked { get; init; } = true;

        /// <summary>True if at least one category is checked.</summary>
        public bool AnySelected => FloorHost || RoofHost || FloorLinked || RoofLinked;

        /// <summary>True if any Host-model category (Floor or Roof) is checked.</summary>
        public bool AnyHost => FloorHost || RoofHost;

        /// <summary>True if any Linked-model category (Floor or Roof) is checked.</summary>
        public bool AnyLinked => FloorLinked || RoofLinked;
    }
}
