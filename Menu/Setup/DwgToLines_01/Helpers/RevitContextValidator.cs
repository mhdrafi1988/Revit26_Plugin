using Autodesk.Revit.UI;

namespace Revit26_Plugin.DwgSymbolicConverter_V01.Helpers
{
    public static class RevitContextValidator
    {
        public static bool IsFamilyEditor(UIApplication app, out string message)
        {
            if (!app.ActiveUIDocument.Document.IsFamilyDocument)
            {
                message = "This tool must be run in the Family Editor.";
                return false;
            }

            message = string.Empty;
            return true;
        }
    }
}
