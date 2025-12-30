using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace Revit26_Plugin.Creaser_V31.Helpers
{
    internal static class RevitContextGuard
    {
        public static bool IsValidPlanView(
            ExternalCommandData data,
            out UIDocument uiDoc,
            out Document doc)
        {
            uiDoc = data.Application.ActiveUIDocument;
            doc = uiDoc?.Document;

            return doc != null &&
                   doc.ActiveView?.ViewType == ViewType.FloorPlan &&
                   doc.ActiveView.SketchPlane != null;
        }
    }
}
