using System;

namespace Revit26_Plugin.ParaManager.V002.Models
{
    /// <summary>
    /// Settings persisted between sessions: last-used shared parameter file path,
    /// last-selected category names, and last log-export folder.
    /// Loaded/saved via the suite-wide <see cref="Revit26_Plugin.Shared.Services.SettingsService{T}"/>.
    /// </summary>
    public class ParaManagerSettings
    {
        public string LastSharedParameterFilePath { get; set; } = string.Empty;
        public string[] LastSelectedCategoryNames { get; set; } = Array.Empty<string>();
        public string LastLogExportFolder { get; set; } = string.Empty;
    }
}
