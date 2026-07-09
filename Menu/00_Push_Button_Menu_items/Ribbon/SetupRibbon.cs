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

            //Workset creation-mange-ment tools
            setup.AddPushButton(new PushButtonData("Btn_CreateWorksetsFromLinkedFiles_V06", "Create Worksets FromLinked Files v06", assemblyPath, "Revit26_Plugin.WorksetManager.V06.CreateWorksetsFromLinkedFilesCommand"));
            setup.AddPushButton(new PushButtonData("Btn_WorksetManager_02", "WorksetManager_02", assemblyPath, " Revit26_Plugin.WorksetManager_02.Commands.WorksetManagerCommand"));


            setup.AddPushButton(new PushButtonData("Btn_WorksetManager_10", "WorksetManager_ v10", assemblyPath, "Revit26_Plugin.WSFL_010.Commands.CreateWorksetsFromLinkedFiles"));
            setup.AddPushButton(new PushButtonData("Btn_WorksetManager_007", "WorksetManager_ 007", assemblyPath, "Revit26_Plugin.WorksetManager_007.CreateWorksetsFromLinkedFilesCommand"));
            setup.AddPushButton(new PushButtonData("Btn_WorksetManager_009", "WorksetManager_ 009", assemblyPath, "Revit26_Plugin.WorksetManager.V009.CreateWorksetsFromLinkedFiles"));
            setup.AddPushButton(new PushButtonData("Btn_WorksetManager_010", "WorksetManager_ 010", assemblyPath, "Revit26_Plugin.WorksetManager.V010.CreateWorksetsFromLinkedFiles"));

            PulldownButton Linker = panel.AddItem(new PulldownButtonData("Batch Link", "Batch Link")) as PulldownButton;
            Linker.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Linker_32.png");

            Linker.AddPushButton(new PushButtonData("BatchLinkDwgCommand", "BatchLinkDwgCommand", assemblyPath, "BatchDwgFamilyLinker.Command.BatchLinkDwgCommand"));
            Linker.AddPushButton(new PushButtonData("DwgSymbolicConverter_V01", "DwgSymbolicConverter_V01", assemblyPath, "Revit26_Plugin.DwgSymbolicConverter_V01.Commands.LaunchCommand"));
            Linker.AddPushButton(new PushButtonData("DwgSymbolicConverter_V02", "DwgSymbolicConverter_V02", assemblyPath, "Revit26_Plugin.DwgSymbolicConverter_V02.Commands.LaunchCommand"));
            Linker.AddPushButton(new PushButtonData("DwgSymbolicConverter_V03", "DwgSymbolicConverter_V03", assemblyPath, "Revit26_Plugin.DwgSymbolicConverter_V03.Commands.LaunchCommand"));
        }
    }
}
