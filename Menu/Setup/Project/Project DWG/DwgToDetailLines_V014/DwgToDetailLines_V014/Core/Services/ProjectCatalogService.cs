// ==============================================
// File: ProjectCatalogService.cs
// Layer: Core/Services
// ADDED in V011: the "Default Line Style" / "Default Fill Pattern" dropdowns
// were hardcoded placeholder strings ("Thin Lines", "Diagonal Crosshatch"...)
// not read from the project at all. This service supplies the real lists,
// using the exact same lookups DetailLineStyleService / DetailFillRegionStyleService
// use to resolve a match, so what's shown in the dropdown is always
// something that can actually resolve.
// ADDED in V014: GetLineStyles / GetFilledRegionTypes (with colour,
// lineweight and pattern for the shortlist auto-matcher) and
// GetLayerAppearances (the same for CAD layers).
// ==============================================

using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;
using Revit26_Plugin.DwgToDetailLines.V014.Core.Models;
using MediaBrush = System.Windows.Media.Brush;
using MediaColor = System.Windows.Media.Color;
using SolidColorBrush = System.Windows.Media.SolidColorBrush;

namespace Revit26_Plugin.DwgToDetailLines.V014.Core.Services
{
    /// <summary>Reads line styles, fill region types and CAD layer appearances from the project.</summary>
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

        /// <summary>
        /// Existing line styles (OST_Lines subcategories) with their colour, lineweight
        /// and pattern, sorted by name.
        /// </summary>
        public static List<StyleOption> GetLineStyles(Document doc)
        {
            Category linesCategory = doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
            if (linesCategory == null)
                return new List<StyleOption>();

            return linesCategory.SubCategories
                .Cast<Category>()
                .Select(c =>
                {
                    CadLayerAppearance a = ReadAppearance(c);
                    return new StyleOption
                    {
                        Name = c.Name,
                        Appearance = a,
                        Swatch = ToBrush(a),
                        Detail = $"LW {(a.LineWeight > 0 ? a.LineWeight.ToString() : "–")} · {(a.IsSolid ? "solid" : "pattern")}"
                    };
                })
                .OrderBy(o => o.Name)
                .ToList();
        }

        /// <summary>
        /// Appearance of each layer of a CAD import, keyed by layer name (the import
        /// category's subcategories, the same names CadGeometryExtractor reports).
        /// Returns an empty map when the import has no category.
        /// </summary>
        public static Dictionary<string, CadLayerAppearance> GetLayerAppearances(ImportInstance import)
        {
            var result = new Dictionary<string, CadLayerAppearance>();
            Category cat = import?.Category;
            if (cat == null)
                return result;

            foreach (Category sub in cat.SubCategories)
                result[sub.Name] = ReadAppearance(sub);

            return result;
        }

        /// <summary>Frozen swatch brush for an appearance (grey when null).</summary>
        public static MediaBrush ToBrush(CadLayerAppearance a)
        {
            var brush = a == null
                ? new SolidColorBrush(MediaColor.FromRgb(160, 160, 160))
                : new SolidColorBrush(MediaColor.FromRgb(a.R, a.G, a.B));
            brush.Freeze();
            return brush;
        }

        /// <summary>
        /// Existing filled region types with their foreground pattern colour and
        /// pattern name, sorted by name.
        /// </summary>
        public static List<StyleOption> GetFilledRegionTypes(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FilledRegionType))
                .Cast<FilledRegionType>()
                .Select(t =>
                {
                    Color colour = t.ForegroundPatternColor;
                    bool valid = colour != null && colour.IsValid;
                    var pattern = doc.GetElement(t.ForegroundPatternId) as FillPatternElement;
                    bool solid = pattern?.GetFillPattern()?.IsSolidFill ?? false;

                    var a = new CadLayerAppearance(
                        valid ? colour.Red : (byte)0,
                        valid ? colour.Green : (byte)0,
                        valid ? colour.Blue : (byte)0,
                        0,
                        solid);

                    return new StyleOption
                    {
                        Name = t.Name,
                        Appearance = a,
                        Swatch = ToBrush(a),
                        Detail = pattern?.Name ?? "no pattern"
                    };
                })
                .OrderBy(o => o.Name)
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

        private static CadLayerAppearance ReadAppearance(Category c)
        {
            Color colour = c.LineColor;
            bool valid = colour != null && colour.IsValid;

            int weight = c.GetLineWeight(GraphicsStyleType.Projection) ?? 0;

            ElementId patternId = c.GetLinePatternId(GraphicsStyleType.Projection);
            bool isSolid = patternId == null
                           || patternId == ElementId.InvalidElementId
                           || patternId == LinePatternElement.GetSolidPatternId();

            return new CadLayerAppearance(
                valid ? colour.Red : (byte)0,
                valid ? colour.Green : (byte)0,
                valid ? colour.Blue : (byte)0,
                weight,
                isSolid);
        }
    }
}
