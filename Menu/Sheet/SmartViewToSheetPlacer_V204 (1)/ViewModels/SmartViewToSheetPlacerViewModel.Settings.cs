using System;
using System.IO;
using System.Text.Json;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.SmartViewToSheetPlacer.V204.Models;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V204.ViewModels
{
    /// <summary>Settings persistence: %AppData%\Revit26_Plugin\SmartViewToSheetPlacer\settings.json,
    /// via System.Text.Json. Loaded once in the ctor; saved from NextToStage1→2
    /// transition and from Window.Closing (V204 — new, see OnWindowClosing).</summary>
    public partial class SmartViewToSheetPlacerViewModel
    {
        private static string SettingsPath =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Revit26_Plugin", ToolName, "settings.json");

        private void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    _settings = JsonSerializer.Deserialize<SmartViewToSheetPlacerSettings>(json) ?? new();
                    MarginTopMm = _settings.LastMarginTopMm;
                    MarginBottomMm = _settings.LastMarginBottomMm;
                    MarginLeftMm = _settings.LastMarginLeftMm;
                    MarginRightMm = _settings.LastMarginRightMm;
                    GapHorizontalMm = _settings.LastGapHorizontalMm;
                    GapVerticalMm = _settings.LastGapVerticalMm;
                }
            }
            catch
            {
                _settings = new SmartViewToSheetPlacerSettings();
            }
        }

        private void SaveSettings()
        {
            try
            {
                _settings.LastTitleblockName = SelectedTitleblock?.Name;
                _settings.LastMarginTopMm = MarginTopMm;
                _settings.LastMarginBottomMm = MarginBottomMm;
                _settings.LastMarginLeftMm = MarginLeftMm;
                _settings.LastMarginRightMm = MarginRightMm;
                _settings.LastGapHorizontalMm = GapHorizontalMm;
                _settings.LastGapVerticalMm = GapVerticalMm;

                var dir = Path.GetDirectoryName(SettingsPath)!;
                Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception ex)
            {
                Logs.Add(new LogEntry(LogLevel.Warning, $"Could not save settings: {ex.Message}"));
            }
        }
    }
}
