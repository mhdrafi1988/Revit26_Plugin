using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.DwgSymbolicConverter_V02.Services
{
    /// <summary>
    /// Resolves Symbolic Line Styles based on CAD layer names.
    /// Creates missing subcategories under Lines if required.
    /// </summary>
    public class SymbolicLineStyleService
    {
        private readonly Document _doc;
        private readonly Category _lineCategory;

        public SymbolicLineStyleService(Document doc)
        {
            _doc = doc;

            // Built-in "Lines" category (parent of all line styles)
            _lineCategory = _doc.Settings.Categories.get_Item(BuiltInCategory.OST_Lines);
        }

        /// <summary>
        /// Gets or creates a symbolic line style matching the CAD layer name.
        /// </summary>
        public GraphicsStyle GetOrCreate(string cadLayerName)
        {
            // Try existing subcategory first
            Category subCat = _lineCategory.SubCategories
                .Cast<Category>()
                .FirstOrDefault(c => c.Name.Equals(cadLayerName));

            if (subCat == null)
            {
                // Create new subcategory for this CAD layer
                subCat = _doc.Settings.Categories.NewSubcategory(_lineCategory, cadLayerName);
            }

            return subCat.GetGraphicsStyle(GraphicsStyleType.Projection);
        }

        /// <summary>
        /// Batch resolve styles for multiple layers.
        /// </summary>
        public Dictionary<string, GraphicsStyle> ResolveAll(IEnumerable<string> layerNames)
        {
            var result = new Dictionary<string, GraphicsStyle>();

            foreach (string layer in layerNames.Distinct())
            {
                result[layer] = GetOrCreate(layer);
            }

            return result;
        }
    }
}
