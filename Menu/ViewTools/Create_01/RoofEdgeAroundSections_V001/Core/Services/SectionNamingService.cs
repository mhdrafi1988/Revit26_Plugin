using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofEdgeSections.V001
{
    /// <summary>
    /// Builds section view names per the confirmed convention:
    ///   {RoofName_or_Id}_{Direction}_Section   e.g. "Roof123_North_Section"
    /// Falls back to "Roof_{ElementId}" when the roof has no Name parameter set.
    /// Also performs the dedup check: if a view with the exact proposed name
    /// already exists in the document, the row is marked AlreadyExists and is
    /// skipped (never overwritten) — per confirmed spec point 14.
    /// </summary>
    public static class SectionNamingService
    {
        public static string GetRoofDisplayName(Element roof)
        {
            string name = roof.Name;
            if (string.IsNullOrWhiteSpace(name) || name == roof.Id.Value.ToString())
                return $"Roof_{roof.Id.Value}";
            return SanitizeForViewName(name);
        }

        public static string BuildSectionViewName(string roofDisplayName, EdgeDirection direction)
            => $"{roofDisplayName}_{direction}_Section";

        /// <summary>
        /// Returns the set of all existing view names in the document (case-sensitive,
        /// matching Revit's own view-name uniqueness rule), for fast dedup lookups.
        /// </summary>
        public static HashSet<string> GetExistingViewNames(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate)
                .Select(v => v.Name)
                .ToHashSet();
        }

        /// <summary>
        /// Revit view names cannot contain: \ : { } [ ] | ; < > ? ` ~
        /// Strip/replace any such characters so the generated name is always valid.
        /// </summary>
        private static string SanitizeForViewName(string raw)
        {
            char[] invalid = { '\\', ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~' };
            foreach (char c in invalid)
                raw = raw.Replace(c, '_');
            return raw;
        }
    }
}
