// ==============================================
// File: SymbolicLineStyleService.cs
// Layer: Services
// Namespace: Revit26_Plugin.DwgSymbolicConverter_V03.Services
// ==============================================

using Autodesk.Revit.DB;
using System.Linq;
using Revit26_Plugin.DwgSymbolicConverter_V03.Models;

namespace Revit26_Plugin.DwgSymbolicConverter_V03.Services
{
    /// <summary>
    /// Resolves Symbolic Line Styles based on CAD layer names.
    /// Handles missing styles via user prompt and caching.
    /// </summary>
    public class SymbolicLineStyleService
    {
        private readonly Document _doc;
        private readonly Category _linesCategory;
        private readonly LineStyleResolutionService _resolver;

        public SymbolicLineStyleService(
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
        /// Gets an existing symbolic line style or resolves a missing one.
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
