using Autodesk.Revit.DB;
using System;
using System.Linq;

namespace Revit22_Plugin.PlanSections.Services
{
    public class SectionNamingService
    {
        private readonly Document _doc;

        public SectionNamingService(Document doc)
        {
            _doc = doc;
        }

        public string EnsureUniqueName(string baseName, ref int dupCount)
        {
            string candidate = baseName;

            if (!NameExists(candidate))
                return candidate;

            dupCount++;
            candidate = baseName + "_dup";

            int n = 2;
            while (NameExists(candidate))
            {
                candidate = baseName + "_dup" + n;
                n++;
            }

            return candidate;
        }

        private bool NameExists(string name)
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSection))
                .Cast<ViewSection>()
                .Any(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public static string SanitizeForName(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;

            var chars = s.ToCharArray();

            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool ok = char.IsLetterOrDigit(c) || c == ' ' || c == '_' || c == '-';
                if (!ok) chars[i] = '_';
            }

            return new string(chars).Trim();
        }

        public string GenerateBaseName(
            ViewPlan plan,
            string sectionPrefix,
            ElementId lineId,
            bool includePlanLevelInName)
        {
            string levelPrefix = "";

            if (includePlanLevelInName && plan.GenLevel != null)
                levelPrefix = SanitizeForName(plan.GenLevel.Name) + "_";

            return $"{levelPrefix}{sectionPrefix}_{lineId.IntegerValue}";
        }
    }
}
