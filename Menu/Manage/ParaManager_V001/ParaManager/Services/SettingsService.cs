using System;
using System.IO;
using System.Text.Json;

namespace Revit26_Plugin.ParaManager.V001.Services
{
    /// <summary>
    /// Settings persisted between sessions: last-used shared parameter file path,
    /// last-selected category names, and last log-export folder.
    /// Plain POCO — System.Text.Json handles serialization.
    /// </summary>
    public class ParaManagerSettings
    {
        public string LastSharedParameterFilePath { get; set; } = string.Empty;
        public string[] LastSelectedCategoryNames { get; set; } = Array.Empty<string>();
        public string LastLogExportFolder { get; set; } = string.Empty;
    }

    /// <summary>
    /// Loads/saves ParaManagerSettings to %AppData%\Revit26_Plugin\ParaManager\settings.json
    /// per the suite's standard per-tool settings convention.
    /// </summary>
    public static class SettingsService
    {
        private static readonly string SettingsFolder =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Revit26_Plugin", "ParaManager");

        private static readonly string SettingsFilePath = Path.Combine(SettingsFolder, "settings.json");

        public static ParaManagerSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsFilePath))
                    return new ParaManagerSettings();

                var json = File.ReadAllText(SettingsFilePath);
                return JsonSerializer.Deserialize<ParaManagerSettings>(json) ?? new ParaManagerSettings();
            }
            catch
            {
                // Corrupt/missing settings file is non-fatal — start with defaults.
                return new ParaManagerSettings();
            }
        }

        public static void Save(ParaManagerSettings settings)
        {
            try
            {
                Directory.CreateDirectory(SettingsFolder);
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFilePath, json);
            }
            catch
            {
                // Best-effort save — a settings write failure should never crash the tool.
            }
        }
    }
}
