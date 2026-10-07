// ==============================================
// File: DetailLineStyleService.cs
// Layer: Core/Services
// Changes vs V013: line styles are looked up once into a dictionary
// instead of scanning every OST_Lines subcategory per layer, and
// GetByName resolves shortlist-mode mappings without any prompt.
// ==============================================

using Autodesk.Revit.DB;
using System.Collections.Generic;
using Revit26_Plugin.DwgToDetailLines.V014.Core.Models;

namespace Revit26_Plugin.DwgToDetailLines.V014.Core.Services
{
    /// <summary>
    /// Resolves Line Styles (GraphicsStyle under OST_Lines) for assignment to
    /// Detail Curve LineStyle parameters, either by CAD layer name (with a
    /// create/skip prompt for missing ones) or by an explicit style name.
    /// </summary>
    public class DetailLineStyleService
    {
        private readonly Document _doc;
        private readonly Category _linesCategory;
        private readonly LineStyleResolutionService _resolver;
        private readonly Dictionary<string, Category> _byName = new();

        /// <summary>Indexes the project's line styles.</summary>
        public DetailLineStyleService(
            Document document,
            LineStyleResolutionService resolver)
        {
            _doc = document;
            _resolver = resolver;

            _linesCategory =
                _doc.Settings.Categories.get_Item(
                    BuiltInCategory.OST_Lines);

            foreach (Category sub in _linesCategory.SubCategories)
                _byName[sub.Name] = sub;
        }

        /// <summary>
        /// Layer Name mode: the line style named exactly like <paramref name="cadLayerName"/>;
        /// when missing, prompts to create it or skip (null).
        /// </summary>
        public GraphicsStyle GetOrResolve(string cadLayerName)
        {
            GraphicsStyle existing = GetByName(cadLayerName);
            if (existing != null)
                return existing;

            MissingLineStyleDecision decision =
                _resolver.Resolve(cadLayerName);

            if (decision == MissingLineStyleDecision.Skip)
                return null;

            Category newSubCategory =
                _doc.Settings.Categories
                    .NewSubcategory(_linesCategory, cadLayerName);
            _byName[cadLayerName] = newSubCategory;

            return newSubCategory.GetGraphicsStyle(
                GraphicsStyleType.Projection);
        }

        /// <summary>Existing line style named <paramref name="styleName"/>, or null. Never prompts or creates.</summary>
        public GraphicsStyle GetByName(string styleName)
        {
            if (string.IsNullOrEmpty(styleName))
                return null;

            return _byName.TryGetValue(styleName, out Category c)
                ? c.GetGraphicsStyle(GraphicsStyleType.Projection)
                : null;
        }
    }
}
