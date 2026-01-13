using Autodesk.Revit.DB;
using System;
using System.Linq;

namespace Revit26_Plugin.CSFL_V07.Services.Naming
{
    public class SectionNamingService
    {
        private readonly Document _doc;

        public SectionNamingService(Document doc)
        {
            _doc = doc;
        }

        public string GenerateName(
            ViewPlan plan,
            string prefix,
            ElementId sourceLineId,
            out bool renamed)
        {
            renamed = false;

            string levelPart = plan?.GenLevel != null
                ? Sanitize(plan.GenLevel.Name) + "_"
                : string.Empty;

            string baseName = $"{levelPart}{prefix}_{sourceLineId.Value}";
            string name = baseName;
            int i = 1;

            while (NameExists(name))
            {
                renamed = true;
                name = $"{baseName}_dup{i++}";
            }

            return name;
        }

        private bool NameExists(string name)
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(ViewSection))
                .Cast<ViewSection>()
                .Any(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        private static string Sanitize(string s)
        {
            return new string(s.Select(c =>
                char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        }
    }
}
