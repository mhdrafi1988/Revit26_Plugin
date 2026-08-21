using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class ManageRibbon
    {
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = app.CreateRibbonPanel(tabName, "Manage");

            PulldownButton Manage = panel.AddItem(new PulldownButtonData("ManageTools", "Manage Tools")) as PulldownButton;
            Manage.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Manage.Manage32.png");

            Manage.AddPushButton(new PushButtonData("Btn_AnnotationOverlapDetection_V002", "Annotation Overlap Detection V002", assemblyPath, "Revit26_Plugin.AnnotationOverlapDetection.V002.Command"));
        }
    }
}
