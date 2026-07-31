using System;
using System.IO;
using System.Text.Json;

namespace Revit26_Plugin.Shared.Services
{
    /// <summary>
    /// Generic per-tool settings persistence to
    /// %AppData%\Revit26_Plugin\{toolFolderName}\settings.json, via System.Text.Json.
    ///
    /// Generalized from DtlLineDim V006's Infrastructure/Helpers/SettingsService.cs
    /// during the V007 shared-infra refactor. Each tool supplies its own settings
    /// POCO as T (kept local to the tool's Core/Models — only load/save mechanics
    /// are shared) and its own folder name (matches the ToolName segment of the
    /// %AppData% path convention).
    ///
    /// Usage:
    ///   var settings = SettingsService&lt;DtlLineDimSettings&gt;.Load("DtlLineDim");
    ///   SettingsService&lt;DtlLineDimSettings&gt;.Save("DtlLineDim", settings);
    /// </summary>
    public static class SettingsService<T> where T : new()
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private static string GetFolderPath(string toolFolderName) =>
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Revit26_Plugin", toolFolderName);

        private static string GetFilePath(string toolFolderName) =>
            Path.Combine(GetFolderPath(toolFolderName), "settings.json");

        public static T Load(string toolFolderName)
        {
            try
            {
                string filePath = GetFilePath(toolFolderName);
                if (!File.Exists(filePath))
                    return new T();

                string json = File.ReadAllText(filePath);
                var settings = JsonSerializer.Deserialize<T>(json, JsonOptions);
                return settings ?? new T();
            }
            catch
            {
                // Corrupt or unreadable settings file — fall back to defaults rather than crash.
                return new T();
            }
        }

        public static void Save(string toolFolderName, T settings)
        {
            try
            {
                string folderPath = GetFolderPath(toolFolderName);
                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                string json = JsonSerializer.Serialize(settings, JsonOptions);
                File.WriteAllText(GetFilePath(toolFolderName), json);
            }
            catch
            {
                // Best-effort persistence — failure to save should never crash the tool.
            }
        }
    }
}
