using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreateSectionsFromDetailLines.V008.Services
{
    /// <summary>
    /// Generates unique section view names, de-duplicating with a "_dupN"
    /// suffix on collision. Unchanged from V07.
    /// </summary>
    public class SectionNamingService
    {
        private readonly HashSet<string> _existingNames;

        public SectionNamingService(Document doc)
        {
            _existingNames = new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSection))
                .Cast<ViewSection>()
                .Select(v => v.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        public string Generate(
            ViewPlan plan,
            string prefix,
            ElementId sourceId,
            out bool renamed)
        {
            renamed = false;

            string level =
                plan?.GenLevel != null
                ? Sanitize(plan.GenLevel.Name) + "_"
                : string.Empty;

            string baseName = $"{level}{prefix}_{sourceId.Value}";
            string name = baseName;
            int i = 1;

            while (_existingNames.Contains(name))
            {
                renamed = true;
                name = $"{baseName}_dup{i++}";
            }

            _existingNames.Add(name);
            return name;
        }

        private static string Sanitize(string s)
            => new string(s.Select(c =>
                char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
    }
}
