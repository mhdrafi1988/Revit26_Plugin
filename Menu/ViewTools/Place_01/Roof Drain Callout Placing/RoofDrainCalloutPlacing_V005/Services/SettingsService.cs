using System;
using System.IO;
using System.Text.Json;
using Revit26_Plugin.RoofDrainCalloutPlacing.V005.Models;

namespace Revit26_Plugin.RoofDrainCalloutPlacing.V005.Services
{
    /// <summary>
    /// Loads/saves RoofDrainCalloutSettings to
    /// %AppData%\Revit26_Plugin\RoofDrainCalloutPlacing\settings.json.
    /// Load failures (missing file, corrupt JSON) fall back to defaults silently —
    /// this is a UX convenience, not critical data, so it never throws to the caller.
    /// </summary>
    public class SettingsService
    {
        private static readonly string SettingsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Revit26_Plugin", "RoofDrainCalloutPlacing", "settings.json");

        public RoofDrainCalloutSettings Load()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                    return new RoofDrainCalloutSettings();

                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<RoofDrainCalloutSettings>(json)
                       ?? new RoofDrainCalloutSettings();
            }
            catch
            {
                // Corrupt or unreadable settings file — fall back to defaults rather than block tool open.
                return new RoofDrainCalloutSettings();
            }
        }

        public void Save(RoofDrainCalloutSettings settings)
        {
            try
            {
                var dir = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Best-effort — a failed save should never crash the tool or block window close.
            }
        }
    }
}
