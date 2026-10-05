using System;
using System.IO;
using System.Text.Json;

namespace Revit26_Plugin.RoofTag.V016
{
    /// <summary>
    /// Load/save of %AppData%\Revit26_Plugin\RoofTag_V016\settings.json.
    /// Never throws — falls back to defaults and reports a warning string.
    /// </summary>
    public static class RoofTagSettingsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        public static string SettingsPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Revit26_Plugin", "RoofTag_V016", "settings.json");

        public static RoofTagSettings Load(out string warning)
        {
            warning = null;
            try
            {
                if (!File.Exists(SettingsPath))
                    return new RoofTagSettings();

                return JsonSerializer.Deserialize<RoofTagSettings>(File.ReadAllText(SettingsPath))
                       ?? new RoofTagSettings();
            }
            catch (Exception ex)
            {
                warning = $"Settings could not be loaded, defaults used: {ex.Message}";
                return new RoofTagSettings();
            }
        }

        public static bool Save(RoofTagSettings settings, out string warning)
        {
            warning = null;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
                File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings, JsonOptions));
                return true;
            }
            catch (Exception ex)
            {
                warning = $"Settings could not be saved: {ex.Message}";
                return false;
            }
        }
    }
}
