using Autodesk.Revit.UI;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.CalloutCOP_V04.Services
{
    public sealed class RevitContextService
    {
        public UIApplication UiApp { get; }
        public UIDocument UiDoc { get; }
        public Document Doc { get; }
        public bool IsValid { get; }
        public string ErrorMessage { get; }

        private RevitContextService(UIApplication uiapp)
        {
            UiApp = uiapp;
            UiDoc = uiapp.ActiveUIDocument;
            Doc = UiDoc?.Document;

            if (UiDoc == null || Doc == null)
            {
                IsValid = false;
                ErrorMessage = "No active Revit document.";
            }
            else
            {
                IsValid = true;
            }
        }

        public static RevitContextService Create(UIApplication uiapp)
            => new RevitContextService(uiapp);
    }
}
