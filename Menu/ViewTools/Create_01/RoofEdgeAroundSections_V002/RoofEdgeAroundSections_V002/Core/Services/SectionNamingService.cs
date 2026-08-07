using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofEdgeSections.V002
{
    /// <summary>
    /// Builds section view names from the user-configured token pattern (Zone, Level,
    /// Name, Line of Direction, Area, Number — see NamingToken), in the order and
    /// on/off state stored in RoofEdgeSectionsSettings.NamingTokens.
    ///
    /// V001 → V002 change: dedup no longer skips on collision. Instead, a D-series
    /// suffix (D1, D2, ...) is appended automatically and the view is still created.
    /// No section is ever left uncreated purely due to a name collision.
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

        /// <summary>
        /// Builds the final, deduplicated section view name from the enabled naming
        /// tokens. existingNames is checked for collisions and MUTATED (the returned
        /// name is added to it) so subsequent calls in the same plan-build pass see
        /// names generated earlier in this run, not just names already in the document.
        /// </summary>
        public static string BuildSectionViewName(
            RoofBase roof,
            string roofDisplayName,
            EdgeDirection direction,
            RoofEdgeSectionsSettings settings,
            int numberInSequence,
            HashSet<string> existingNames,
            out bool wasRenamedForDuplicate)
        {
            string baseName = BuildBaseName(roof, roofDisplayName, direction, settings, numberInSequence);
            string separator = string.IsNullOrEmpty(settings.NamingSeparator) ? "_" : settings.NamingSeparator;

            string finalName = baseName;
            int dupIndex = 0;
            while (existingNames.Contains(finalName))
            {
                dupIndex++;
                finalName = $"{baseName}{separator}D{dupIndex}";
            }

            wasRenamedForDuplicate = dupIndex > 0;
            existingNames.Add(finalName);
            return finalName;
        }

        private static string BuildBaseName(
            RoofBase roof,
            string roofDisplayName,
            EdgeDirection direction,
            RoofEdgeSectionsSettings settings,
            int numberInSequence)
        {
            var parts = new List<string>();

            foreach (NamingToken token in settings.NamingTokens
                         .Where(t => t.IsEnabled)
                         .OrderBy(t => t.Order))
            {
                string value = ResolveToken(token.Type, roof, roofDisplayName, direction, numberInSequence);
                if (!string.IsNullOrWhiteSpace(value))
                    parts.Add(value);
            }

            // Guard: if every enabled token resolved to blank (e.g. all tokens disabled,
            // or the only enabled tokens are Zone/Area and neither is set on this roof),
            // parts is empty and the name would be "" — which Revit rejects at
            // ViewSection creation time. Fall back to a name that's always non-empty.
            if (parts.Count == 0)
            {
                parts.Add(roofDisplayName);
                parts.Add(direction.ToString());
            }

            // Tokens like LineOfDirection contain literal spaces ("Line of North"); replace
            // them with the configured separator so the built name matches what the live
            // preview in the UI shows (which does the same replacement) and reads as one
            // consistent, separator-joined string rather than mixing spaces and separators.
            string separator = string.IsNullOrEmpty(settings.NamingSeparator) ? "_" : settings.NamingSeparator;
            string joined = string.Join(separator, parts).Replace(" ", separator);
            return SanitizeForViewName(joined);
        }

        private static string ResolveToken(
            NamingTokenType type,
            RoofBase roof,
            string roofDisplayName,
            EdgeDirection direction,
            int numberInSequence)
        {
            switch (type)
            {
                case NamingTokenType.Zone:
                    return GetZoneValue(roof); // skipped (null) if blank — no placeholder text

                case NamingTokenType.Level:
                    return GetLevelName(roof);

                case NamingTokenType.Name:
                    return roofDisplayName;

                case NamingTokenType.LineOfDirection:
                    return $"Line of {direction}";

                case NamingTokenType.Area:
                    return GetAreaValue(roof);

                case NamingTokenType.Number:
                    return numberInSequence.ToString("D2");

                default:
                    return null;
            }
        }

        /// <summary>
        /// Looks up the roof's "Zone" parameter by exact name, instance first, falling
        /// back to the roof's type. Returns null if not found or blank — the token then
        /// contributes nothing to the name (no placeholder text), per confirmed spec.
        /// </summary>
        private static string GetZoneValue(RoofBase roof)
        {
            Parameter p = roof.LookupParameter("Zone");
            string value = p?.AsString() ?? p?.AsValueString();

            if (string.IsNullOrWhiteSpace(value))
            {
                ElementId typeId = roof.GetTypeId();
                if (typeId != ElementId.InvalidElementId)
                {
                    Element roofType = roof.Document.GetElement(typeId);
                    Parameter typeParam = roofType?.LookupParameter("Zone");
                    value = typeParam?.AsString() ?? typeParam?.AsValueString();
                }
            }

            return string.IsNullOrWhiteSpace(value) ? null : SanitizeForViewName(value);
        }

        private static string GetLevelName(RoofBase roof)
        {
            ElementId levelId = roof.LevelId;
            if (levelId == null || levelId == ElementId.InvalidElementId)
                return null;

            Level level = roof.Document.GetElement(levelId) as Level;
            return level != null ? SanitizeForViewName(level.Name) : null;
        }

        /// <summary>
        /// Roof's computed area, rounded to whole units, with a unit suffix matching the
        /// document's display units (m2 or ft2).
        /// </summary>
        private static string GetAreaValue(RoofBase roof)
        {
            Parameter areaParam = roof.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
            if (areaParam == null)
                return null;

            double areaInternal = areaParam.AsDouble(); // internal units = sq ft

            ForgeTypeId unitTypeId = roof.Document.GetUnits()
                .GetFormatOptions(SpecTypeId.Area).GetUnitTypeId();

            bool isMetric = unitTypeId == UnitTypeId.SquareMeters;
            double displayArea = isMetric
                ? UnitUtils.ConvertFromInternalUnits(areaInternal, UnitTypeId.SquareMeters)
                : UnitUtils.ConvertFromInternalUnits(areaInternal, UnitTypeId.SquareFeet);

            string suffix = isMetric ? "m2" : "ft2";
            return $"{displayArea:F0}{suffix}";
        }

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
