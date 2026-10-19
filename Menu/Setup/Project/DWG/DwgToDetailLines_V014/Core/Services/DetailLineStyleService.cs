// ==============================================
// File: DetailLineStyleService.cs
// Layer: Core/Services
// ==============================================

using Autodesk.Revit.DB;
using System.Linq;
using Revit26_Plugin.DwgToDetailLines.V014.Core.Models;

namespace Revit26_Plugin.DwgToDetailLines.V014.Core.Services
{
    /// <summary>
    /// Resolves Line Styles (GraphicsStyle under OST_Lines) based on CAD layer names,
    /// for assignment to Detail Curve LineStyle parameters.
    /// Handles missing styles via user prompt and caching.
    /// </summary>
    public class DetailLineStyleService
    {
        private readonly Document _doc;
        private readonly Category _linesCategory;
        private readonly LineStyleResolutionService _resolver;

        public DetailLineStyleService(
            Document document,
            LineStyleResolutionService resolver)
        {
            _doc = document;
            _resolver = resolver;

            _linesCategory =
                _doc.Settings.Categories.get_Item(
                    BuiltInCategory.OST_Lines);
        }

        /// <summary>
        /// Returns the line style for a CAD layer. When <paramref name="mappedStyleName"/> names an
        /// existing line style it is used directly (no prompt); the match-layer-name entry, null,
        /// or a style that no longer exists falls back to the V013 lookup by layer name.
        /// </summary>
        public GraphicsStyle GetOrResolve(string cadLayerName, string mappedStyleName = null)
        {
            if (!string.IsNullOrEmpty(mappedStyleName) && mappedStyleName != LayerRow.MatchLayerName)
            {
                Category mapped = _linesCategory.SubCategories
                    .Cast<Category>()
                    .FirstOrDefault(c => c.Name.Equals(mappedStyleName));

                if (mapped != null)
                    return mapped.GetGraphicsStyle(GraphicsStyleType.Projection);
            }

            Category subCategory =
                _linesCategory.SubCategories
                    .Cast<Category>()
                    .FirstOrDefault(c =>
                        c.Name.Equals(cadLayerName));

            if (subCategory != null)
            {
                return subCategory.GetGraphicsStyle(
                    GraphicsStyleType.Projection);
            }

            MissingLineStyleDecision decision =
                _resolver.Resolve(cadLayerName);

            if (decision == MissingLineStyleDecision.Skip)
                return null;

            Category newSubCategory =
                _doc.Settings.Categories
                    .NewSubcategory(_linesCategory, cadLayerName);

            return newSubCategory.GetGraphicsStyle(
                GraphicsStyleType.Projection);
        }
    }
}
