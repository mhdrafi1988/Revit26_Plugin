namespace Revit26_Plugin.RoofViewFocus.V002.Core.Models
{
    /// <summary>Display + lookup data for one selected roof.</summary>
    public sealed class RoofInfo
    {
        /// <summary>Revit ElementId value (display / logging only).</summary>
        public long Id { get; init; }

        /// <summary>Stable lookup key used by the ExternalEvent handler.</summary>
        public string UniqueId { get; init; } = string.Empty;

        /// <summary>e.g. "Basic Roof : Generic - 300mm".</summary>
        public string Description { get; init; } = string.Empty;
    }
}
