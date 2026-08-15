using Autodesk.Revit.UI;

namespace Revit26_Plugin.SectionManager.V008.Helpers
{
    public static class RevitContextGuard
    {
        public static bool HasActiveDocument(UIApplication app)
        {
            return app?.ActiveUIDocument?.Document != null;
        }
    }
}
