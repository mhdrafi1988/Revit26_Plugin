// ==============================================
// File: ProjectCatalogService.cs
// Layer: Core/Services
// ADDED in V011: the "Default Line Style" / "Default Fill Pattern" dropdowns
// were hardcoded placeholder strings ("Thin Lines", "Diagonal Crosshatch"...)
// not read from the project at all. This service supplies the real lists,
// using the exact same lookups DetailLineStyleService / DetailFillRegionStyleService
// use to resolve a match, so what's shown in the dropdown is always
// something that can actually resolve.
// ==============================================

using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.DwgToDetailLines.V011.Core.Services
{
    public static class ProjectCatalogService
    {
        /// <summary>Existing line style names (OST_Lines subcategories) in this project.</summary>
        public static List<string> GetLineStyleNames(Document doc)
        {
            Category linesCategory = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);

            return linesCategory.SubCategories
                .Cast<Category>()
                .Select(c => c.Name)
                .OrderBy(n => n)
                .ToList();
        }

        /// <summary>Existing FilledRegionType names in this project.</summary>
        public static List<string> GetFilledRegionTypeNames(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FilledRegionType))
                .Cast<FilledRegionType>()
                .Select(t => t.Name)
                .OrderBy(n => n)
                .ToList();
        }
    }
}
