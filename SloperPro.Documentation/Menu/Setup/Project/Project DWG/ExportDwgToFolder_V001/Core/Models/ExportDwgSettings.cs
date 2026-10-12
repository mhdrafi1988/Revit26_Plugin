namespace Revit26_Plugin.ExportDwgToFolder.V001.Core.Models
{
    /// <summary>
    /// Persisted settings for Export DWG to Folder.
    /// Serialised by <see cref="Revit26_Plugin.Shared.Services.SettingsService{T}"/>.
    /// Version field guards against schema changes in future releases.
    /// </summary>
    public class ExportDwgSettings
    {
        public int Version { get; set; } = 1;

        public string LastOutputFolder { get; set; } = string.Empty;

        // Source toggles
        public bool ExportLinked { get; set; } = true;
        public bool ExportImported { get; set; } = true;

        // View-type subfolder toggles
        public bool SubfoldersByViewType { get; set; } = true;
        public bool IncludePlan { get; set; } = true;
        public bool IncludeSection { get; set; } = true;
        public bool IncludeDrafting { get; set; } = true;
        public bool IncludeNoViews { get; set; } = true;

        // Other options
        public bool GenerateSidecar { get; set; } = true;
        public bool Overwrite { get; set; } = false;
    }
}
