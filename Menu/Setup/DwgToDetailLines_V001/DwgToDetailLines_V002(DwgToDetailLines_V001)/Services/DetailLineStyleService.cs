// ==============================================
// File: DetailLineStyleService.cs
// Layer: Services
// Namespace: Revit26_Plugin.DwgToDetailLines.V002.Services
// ==============================================

using Autodesk.Revit.DB;
using System.Linq;
using Revit26_Plugin.DwgToDetailLines.V002.Models;

namespace Revit26_Plugin.DwgToDetailLines.V002.Services
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

            // Built-in parent category for all line styles
            _linesCategory =
                _doc.Settings.Categories.get_Item(
                    BuiltInCategory.OST_Lines);
        }

        /// <summary>
        /// Gets an existing line style or resolves a missing one.
        /// Returns null if the user chooses to skip this CAD layer.
        /// </summary>
        public GraphicsStyle GetOrResolve(string cadLayerName)
        {
            // Try existing subcategory first
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

            // Ask user how to handle missing layer
            MissingLineStyleDecision decision =
                _resolver.Resolve(cadLayerName);

            if (decision == MissingLineStyleDecision.Skip)
                return null;

            // Create new subcategory under Lines
            Category newSubCategory =
                _doc.Settings.Categories
                    .NewSubcategory(_linesCategory, cadLayerName);

            return newSubCategory.GetGraphicsStyle(
                GraphicsStyleType.Projection);
        }
    }
}
