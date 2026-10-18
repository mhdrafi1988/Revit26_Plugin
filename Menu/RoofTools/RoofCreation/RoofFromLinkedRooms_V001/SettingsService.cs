using System;
using System.IO;
using System.Text.Json;

namespace Revit26_Plugin.RoofFromLinkedRooms.V001
{
    /// <summary>
    /// Loads/saves <see cref="ToolSettings"/> as JSON. Failures are non-fatal: a missing or
    /// corrupt file yields defaults and a failed save is reported through the out message.
    /// </summary>
    public static class SettingsService
    {
        private static readonly string Folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Revit26_Plugin", "RoofFromLinkedRooms");

        private static readonly string FilePath = Path.Combine(Folder, "settings.json");
        private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

        /// <summary>Returns loaded settings, or defaults when no valid file exists.</summary>
        public static ToolSettings Load(out string message)
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    message = "No saved settings found — using defaults.";
                    return new ToolSettings();
                }

                var settings = JsonSerializer.Deserialize<ToolSettings>(File.ReadAllText(FilePath));
                if (settings == null)
                {
                    message = "Settings file was empty — using defaults.";
                    return new ToolSettings();
                }

                message = $"Settings loaded from {FilePath}";
                return settings;
            }
            catch (Exception ex)
            {
                message = $"Could not read settings ({ex.Message}) — using defaults.";
                return new ToolSettings();
            }
        }

        /// <summary>Saves settings; returns false (reason in message) on failure.</summary>
        public static bool Save(ToolSettings settings, out string message)
        {
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
                message = $"Settings saved to {FilePath}";
                return true;
            }
            catch (Exception ex)
            {
                message = $"Could not save settings: {ex.Message}";
                return false;
            }
        }
    }
}
