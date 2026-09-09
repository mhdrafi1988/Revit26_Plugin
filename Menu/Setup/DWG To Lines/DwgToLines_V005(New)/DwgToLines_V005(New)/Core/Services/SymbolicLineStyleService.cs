using Autodesk.Revit.DB;
using System.Linq;
using Revit26_Plugin.DwgToLines.V005.Core.Models;

namespace Revit26_Plugin.DwgToLines.V005.Core.Services
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

            _linesCategory =
                _doc.Settings.Categories.get_Item(
                    BuiltInCategory.OST_Lines);
        }

        /// <summary>
        /// Gets an existing line style (GraphicsStyle) for the given CAD layer name,
        /// or resolves a missing one via the resolver. Returns null if the user
        /// chooses to skip this CAD layer.
        /// </summary>
        public GraphicsStyle GetOrResolve(string cadLayerName)
        {
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
