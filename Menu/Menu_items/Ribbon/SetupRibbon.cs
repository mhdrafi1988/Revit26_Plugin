using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
//using Revit22_Plugin.Utils;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class SetupRibbon
    {
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = app.CreateRibbonPanel(tabName, "Setup Tools");
            PulldownButton setup = panel.AddItem(new PulldownButtonData("SetupTools", "Setup Tools")) as PulldownButton;
            setup.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Setting32.png");

            setup.AddPushButton(new PushButtonData("Btn_CreateWorksetsFromLinkedFilesv022", "Create Worksets FromLinked Files v02", assemblyPath, "Revit26_Plugin.WSAV02.CreateWorksetsFromLinkedFilesv02"));
            setup.AddPushButton(new PushButtonData("Btn_CreateWorksetsFromLinkedFiles_V05", "Create Worksets FromLinked Files v05", assemblyPath, "Revit26_Plugin.WSA_V05.Commands.CreateWorksetsFromLinkedFilesV05"));

        }
    }
}
