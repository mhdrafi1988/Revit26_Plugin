using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.Services
{
    /// <summary>
    /// Selection filter that allows ONLY RoofBase elements.
    /// Used during user selection to prevent invalid picks.
    /// </summary>
    public class RoofSelectionFilter : ISelectionFilter
    {
        /// <summary>
        /// Allow selection of roof elements only.
        /// </summary>
        public bool AllowElement(Element elem)
        {
            return elem is RoofBase;
        }

        /// <summary>
        /// Reference-level filtering is not required here.
        /// </summary>
        public bool AllowReference(Reference reference, XYZ position)
        {
            return true;
        }
    }
}
