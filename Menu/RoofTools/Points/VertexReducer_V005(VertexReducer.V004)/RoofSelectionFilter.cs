using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace Revit26_Plugin.VertexReducer.V005.Commands
{
    public class RoofSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            if (!(elem is FootPrintRoof roof)) return false;
            try
            {
                var slabShapeEditor = roof.GetSlabShapeEditor();
                return slabShapeEditor != null && slabShapeEditor.IsEnabled;
            }
            catch
            {
                return false;
            }
        }

        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
