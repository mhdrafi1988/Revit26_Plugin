using Autodesk.Revit.UI;

namespace Revit26_Plugin.SectionManager_V07.Helpers
{
    public static class RevitContextGuard
    {
        public static bool HasActiveDocument(UIApplication app)
        {
            return app?.ActiveUIDocument?.Document != null;
        }
    }
}
