using System;
using System.IO;
using System.Text.Json;
using Revit26_Plugin.RoofDrainCalloutPlacingVByDrain.V005.Models;

namespace Revit26_Plugin.RoofDrainCalloutPlacingVByDrain.V005.Services
{
    /// <summary>
    /// Persists RoofDrainCalloutSettings to %AppData%\Revit26_Plugin\RoofDrainCalloutPlacing\VByDrain.V004\settings.json
    /// Loads on window open, saves on close or after successful run.
    /// V004: settings now include per-group (Circle/Rectangle/Other) sizing config
    /// instead of a single global offset/margin/floor.
    /// </summary>
    public class SettingsService
    {
        private static readonly string SettingsDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Revit26_Plugin",
            "RoofDrainCalloutPlacing",
            "VByDrain.V004");

        private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        /// <summary>Load settings from disk, or return defaults if file doesn't exist.</summary>
        public static RoofDrainCalloutSettings LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<RoofDrainCalloutSettings>(json, JsonOptions)
                        ?? new RoofDrainCalloutSettings();
                }
            }
            catch { }

            return new RoofDrainCalloutSettings();
        }

        /// <summary>Save settings to disk.</summary>
        public void SaveSettings(RoofDrainCalloutSettings settings)
        {
            try
            {
                Directory.CreateDirectory(SettingsDir);
                var json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(SettingsPath, json);
            }
            catch { }
        }
    }
}
