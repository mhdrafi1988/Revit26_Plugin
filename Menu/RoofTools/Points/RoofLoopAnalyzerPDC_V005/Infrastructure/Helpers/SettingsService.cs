// SettingsService.cs
// Namespace: Revit26_Plugin.RoofLoopAnalyzerPDC.V005
// Per-tool JSON settings at %AppData%\Revit26_Plugin\RoofLoopAnalyzerPDC\settings.json.
// Never throws — failures are reported through the caller's log sink as a
// Warning and the tool falls back to defaults.

using Revit26_Plugin.RoofLoopAnalyzerPDC.V005.Core.Models;
using System;
using System.IO;
using System.Text.Json;

namespace Revit26_Plugin.RoofLoopAnalyzerPDC.V005.Infrastructure.Helpers
{
    public static class SettingsService
    {
        private const string ToolName = "RoofLoopAnalyzerPDC";

        private static string Folder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Revit26_Plugin", ToolName);

        private static string FilePath => Path.Combine(Folder, "settings.json");

        private static readonly JsonSerializerOptions Options =
            new JsonSerializerOptions { WriteIndented = true };

        /// <summary>Returns null when no settings file exists or it cannot be read.</summary>
        public static RoofLoopAnalyzerPDCSettings Load(Action<string> warn = null)
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                var json = File.ReadAllText(FilePath);
                if (string.IsNullOrWhiteSpace(json)) return null;
                return JsonSerializer.Deserialize<RoofLoopAnalyzerPDCSettings>(json);
            }
            catch (Exception ex)
            {
                warn?.Invoke($"Could not load settings ({ex.GetType().Name}: {ex.Message}). Using defaults.");
                return null;
            }
        }

        /// <summary>Silent-skip on failure — persistence must never block the tool.</summary>
        public static void Save(RoofLoopAnalyzerPDCSettings settings, Action<string> warn = null)
        {
            if (settings == null) return;
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, Options));
            }
            catch (Exception ex)
            {
                warn?.Invoke($"Could not save settings ({ex.GetType().Name}: {ex.Message}).");
            }
        }
    }
}
