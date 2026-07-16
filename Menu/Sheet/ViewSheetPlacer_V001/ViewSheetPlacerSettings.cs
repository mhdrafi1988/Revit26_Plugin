using System;
using System.IO;
using System.Text.Json;

namespace Revit26_Plugin.Tools.ViewSheetPlacer
{
    /// <summary>
    /// User settings persisted to
    /// %AppData%\Revit26_Plugin\ViewSheetPlacer\settings.json.
    /// </summary>
    public sealed class ViewSheetPlacerSettings
    {
        public string TitleblockUniqueId { get; set; } = string.Empty;
        public string SheetNamePrefix { get; set; } = string.Empty;
        public string Grouping { get; set; } = "Discipline"; // "Discipline" | "ViewType"
        public bool SkipAlreadyPlaced { get; set; } = true;
        public bool ShowViewportTitles { get; set; } = true;

        // Layout tuning (millimetres on paper). Exposed here so they can be
        // adjusted without touching code; wire to UI later if needed.
        public double SheetMarginMm { get; set; } = 15.0;
        public double ViewportGapMm { get; set; } = 10.0;

        // Right-side reserve for the titleblock title band. Default 0 keeps the
        // previous behaviour; set per-titleblock (e.g. ~180mm for a right strip).
        public double TitleStripMm { get; set; } = 0.0;

        private const string ToolName = "ViewSheetPlacer";

        private static string SettingsPath
        {
            get
            {
                string dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "Revit26_Plugin", ToolName);
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "settings.json");
            }
        }

        public static ViewSheetPlacerSettings Load()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    string json = File.ReadAllText(SettingsPath);
                    return JsonSerializer.Deserialize<ViewSheetPlacerSettings>(json)
                           ?? new ViewSheetPlacerSettings();
                }
            }
            catch
            {
                // Corrupt/unreadable settings fall back to defaults.
            }
            return new ViewSheetPlacerSettings();
        }

        public void Save()
        {
            try
            {
                string json = JsonSerializer.Serialize(
                    this, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch
            {
                // Non-fatal: settings just won't persist this run.
            }
        }
    }
}
