using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Revit26_Plugin.Shared.Models;
using Revit26_Plugin.SmartViewToSheetPlacer.V213.Models;

namespace Revit26_Plugin.SmartViewToSheetPlacer.V213.ViewModels
{
    /// <summary>Settings persistence: %AppData%\Revit26_Plugin\SmartViewToSheetPlacer\settings.json,
    /// via System.Text.Json. Loaded once in the ctor; saved from NextToStage1→2
    /// transition and from Window.Closing (V213 — new, see OnWindowClosing).</summary>
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

                    if (Enum.TryParse<Models.ReadingDirection>(_settings.LastReadingDirection, out var rd))
                        ReadingDirection = rd;
                    if (Enum.TryParse<Models.RowTiebreak>(_settings.LastRowTiebreak, out var tb))
                        RowTiebreak = tb;
                    YToleranceMm = _settings.LastYToleranceMm;
                    IsActivityLogExpanded = _settings.IsActivityLogExpanded;

                    // Per-group gap settings are restored later, once the actual
                    // ViewType groups for this run are known — see
                    // RestorePersistedGapSettings(), called from
                    // EnsureGapSettingsGroups() in Stage2.
                }
            }
            catch
            {
                _settings = new SmartViewToSheetPlacerSettings();
            }
        }

        /// <summary>
        /// Matches a newly-created ViewGroupGapSettings against any previously
        /// saved entry for the same ViewType (by name) and restores its style/
        /// values. Called once per group, right after construction, from
        /// EnsureGapSettingsGroups() — mirrors the View Type filter checkbox
        /// restore-by-match pattern in Stage 1.
        /// </summary>
        private void RestorePersistedGapSettings(ViewGroupGapSettings group)
        {
            var saved = _settings.GapSettingsByType
                .FirstOrDefault(g => g.ViewTypeName == group.RevitViewType.ToString());
            if (saved == null) return;

            if (Enum.TryParse<Models.GapStyle>(saved.GapStyle, out var style))
                group.GapStyle = style;
            group.HorizontalGapMm = saved.HorizontalGapMm;
            group.VerticalGapMm = saved.VerticalGapMm;
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

                _settings.LastReadingDirection = ReadingDirection.ToString();
                _settings.LastRowTiebreak = RowTiebreak.ToString();
                _settings.LastYToleranceMm = YToleranceMm;
                _settings.IsActivityLogExpanded = IsActivityLogExpanded;

                // V213 fix: merge rather than overwrite — GapSettingsGroups only
                // contains entries for ViewTypes selected in THIS session. A plain
                // overwrite would silently discard any previously-saved entry for a
                // ViewType not selected this time (e.g. Elevation configured last
                // session, no Elevation views selected today) — merge preserves
                // those untouched, same "restore/preserve by match" principle as
                // the View Type filter checkbox persistence in Stage 1.
                var currentSessionEntries = GapSettingsGroups
                    .Select(g => new PersistedGapSetting
                    {
                        ViewTypeName = g.RevitViewType.ToString(),
                        GapStyle = g.GapStyle.ToString(),
                        HorizontalGapMm = g.HorizontalGapMm,
                        VerticalGapMm = g.VerticalGapMm
                    })
                    .ToList();

                var currentSessionTypeNames = currentSessionEntries.Select(e => e.ViewTypeName).ToHashSet();
                var untouchedFromPriorSessions = _settings.GapSettingsByType
                    .Where(e => !currentSessionTypeNames.Contains(e.ViewTypeName));

                _settings.GapSettingsByType = currentSessionEntries.Concat(untouchedFromPriorSessions).ToList();

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
