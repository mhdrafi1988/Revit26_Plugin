using System.Collections.Generic;

namespace Revit26_Plugin.DwgToDetailLines.V014.Core.Models
{
    /// <summary>
    /// Persisted settings for DWG To Detail Lines (ADDED in V014), stored at
    /// %AppData%\Revit26_Plugin\DwgToDetailLines\settings.json through
    /// <see cref="Revit26_Plugin.Shared.Services.SettingsService{T}"/>.
    /// <see cref="Version"/> guards future schema changes.
    /// </summary>
    public class DwgToDetailLinesSettings
    {
        /// <summary>Settings schema version.</summary>
        public int Version { get; set; } = 1;

        /// <summary>Line style mapping mode last used.</summary>
        public LineStyleMappingMode MappingMode { get; set; } = LineStyleMappingMode.Shortlist;

        /// <summary>Offset converted elements to the right of the CAD by one bounding-box width.</summary>
        public bool PlaceBesideCad { get; set; } = true;

        /// <summary>Names of the shortlisted line styles.</summary>
        public List<string> Shortlist { get; set; } = new();

        /// <summary>CAD layer name → line style name, remembered from previous conversions.</summary>
        public Dictionary<string, string> LayerMappings { get; set; } = new();
    }
}
