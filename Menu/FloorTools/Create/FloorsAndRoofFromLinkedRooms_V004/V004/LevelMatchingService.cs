using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V004
{
    /// <summary>
    /// Loads host-model levels for the "New Level" dropdown and auto-matches each linked
    /// room's level name against them. Matching is case-insensitive and trims leading/
    /// trailing whitespace before comparing (confirmed spec) — e.g. "Level 1" matches
    /// " level 1 " but NOT "Level 1 Mezzanine" (no partial/fuzzy matching).
    /// </summary>
    public static class LevelMatchingService
    {
        /// <summary>Host Levels available as mapping targets, excluding template/legend-only
        /// levels (Level.IsTemplate) per confirmed spec. Structural-only levels are included.</summary>
        public static List<HostLevelOption> GetHostLevels(Document hostDoc)
        {
            return new FilteredElementCollector(hostDoc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .Where(l => !l.IsTemplate)
                .OrderBy(l => l.Elevation)
                .Select(l => new HostLevelOption { Name = l.Name, LevelElement = l })
                .ToList();
        }

        /// <summary>Case-insensitive, trimmed exact-name match. Returns null (leaves the
        /// room unmapped) when no host level shares that name — never falls back to a
        /// default level, per confirmed spec.</summary>
        public static HostLevelOption FindMatch(string linkedLevelName, IEnumerable<HostLevelOption> hostLevels)
        {
            if (string.IsNullOrWhiteSpace(linkedLevelName)) return null;

            string normalized = linkedLevelName.Trim();
            return hostLevels.FirstOrDefault(h =>
                string.Equals(h.Name?.Trim(), normalized, StringComparison.OrdinalIgnoreCase));
        }
    }
}
