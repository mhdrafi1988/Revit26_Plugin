using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.DetailLineClosedLoop.V001.Infrastructure.SelectionFilters
{
    /// <summary>Restricts PickObjects to Detail Curves (Detail Lines/Arcs) only — no Model Lines, no other categories.</summary>
    public class DetailCurveSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) =>
            elem is DetailCurve dc && dc.CurveElementType == CurveElementType.DetailCurve;

        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
