using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoofEdgeElementSections.V001
{
    /// <summary>
    /// Builds section view names from the user-configured token pattern (Zone, Level,
    /// Name, Line of Direction, Category, Area, Number — see NamingToken), in the
    /// order and on/off state stored in RoofEdgeElementSectionsSettings.NamingTokens.
    /// Adapted from RoofEdgeAroundSections_V004's SectionNamingService, with a new
    /// Category token resolved from the matched cluster's category.
    ///
    /// Dedup: no row is ever skipped on a name collision — a D-series suffix
    /// (D1, D2, ...) is appended automatically instead.
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
            string matchedCategoryName,
            RoofEdgeElementSectionsSettings settings,
            int numberInSequence,
            HashSet<string> existingNames,
            out bool wasRenamedForDuplicate)
        {
            string baseName = BuildBaseName(roof, roofDisplayName, direction, matchedCategoryName, settings, numberInSequence);
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
            string matchedCategoryName,
            RoofEdgeElementSectionsSettings settings,
            int numberInSequence)
        {
            var parts = new List<string>();

            foreach (NamingToken token in settings.NamingTokens
                         .Where(t => t.IsEnabled)
                         .OrderBy(t => t.Order))
            {
                string value = ResolveToken(token.Type, roof, roofDisplayName, direction, matchedCategoryName, numberInSequence);
                if (!string.IsNullOrWhiteSpace(value))
                    parts.Add(value);
            }

            if (parts.Count == 0)
            {
                parts.Add(roofDisplayName);
                parts.Add(direction.ToString());
            }

            string separator = string.IsNullOrEmpty(settings.NamingSeparator) ? "_" : settings.NamingSeparator;
            string joined = string.Join(separator, parts).Replace(" ", separator);
            return SanitizeForViewName(joined);
        }

        private static string ResolveToken(
            NamingTokenType type,
            RoofBase roof,
            string roofDisplayName,
            EdgeDirection direction,
            string matchedCategoryName,
            int numberInSequence)
        {
            switch (type)
            {
                case NamingTokenType.Zone:
                    return GetZoneValue(roof);

                case NamingTokenType.Level:
                    return GetLevelName(roof);

                case NamingTokenType.Name:
                    return roofDisplayName;

                case NamingTokenType.LineOfDirection:
                    return $"Line of {direction}";

                case NamingTokenType.Category:
                    return string.IsNullOrWhiteSpace(matchedCategoryName) ? null : SanitizeForViewName(matchedCategoryName);

                case NamingTokenType.Area:
                    return GetAreaValue(roof);

                case NamingTokenType.Number:
                    return numberInSequence.ToString("D2");

                default:
                    return null;
            }
        }

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

        private static string GetAreaValue(RoofBase roof)
        {
            Parameter areaParam = roof.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
            if (areaParam == null)
                return null;

            double areaInternal = areaParam.AsDouble();

            ForgeTypeId unitTypeId = roof.Document.GetUnits()
                .GetFormatOptions(SpecTypeId.Area).GetUnitTypeId();

            bool isMetric = unitTypeId == UnitTypeId.SquareMeters;
            double displayArea = isMetric
                ? UnitUtils.ConvertFromInternalUnits(areaInternal, UnitTypeId.SquareMeters)
                : UnitUtils.ConvertFromInternalUnits(areaInternal, UnitTypeId.SquareFeet);

            string suffix = isMetric ? "m2" : "ft2";
            return $"{displayArea:F0}{suffix}";
        }

        public static HashSet<string> GetExistingViewNames(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .Where(v => !v.IsTemplate)
                .Select(v => v.Name)
                .ToHashSet();
        }

        private static string SanitizeForViewName(string raw)
        {
            char[] invalid = { '\\', ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~' };
            foreach (char c in invalid)
                raw = raw.Replace(c, '_');
            return raw;
        }
    }
}
