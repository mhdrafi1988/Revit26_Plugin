using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.RefSectionHeadPlacer.V001.Infrastructure.Helpers
{
    /// <summary>
    /// Small geometry/document utility helpers shared across this tool's
    /// services and ViewModel. Kept separate from the Core/Services classes
    /// since these are generic helpers, not part of the placement pipeline
    /// itself.
    /// </summary>
    public static class GeometryHelper
    {
        /// <summary>
        /// Lists all Section-family ViewFamilyTypes in the document, for
        /// populating the "Section type" ComboBox at the top of the window.
        /// </summary>
        public static List<ViewFamilyType> GetSectionViewFamilyTypes(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .Where(vft => vft.ViewFamily == ViewFamily.Section)
                .OrderBy(vft => vft.Name)
                .ToList();
        }

        /// <summary>
        /// Normalizes a vector, falling back to the given default direction
        /// if the input is zero-length (guards against degenerate
        /// FacingOrientation/HandOrientation values on malformed instances).
        /// </summary>
        public static XYZ SafeNormalize(XYZ vector, XYZ fallback)
        {
            if (vector == null || vector.IsAlmostEqualTo(XYZ.Zero))
                return fallback.Normalize();

            return vector.Normalize();
        }
    }
}
