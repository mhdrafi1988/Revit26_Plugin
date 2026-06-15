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
            setup.AddPushButton(new PushButtonData("Btn_CreateWorksetsFromLinkedFilesv023", "Create Worksets FromLinked Files v03", assemblyPath, "Revit26_Plugin.WSAV03.CreateWorksetsFromLinkedFilesv03"));
            setup.AddPushButton(new PushButtonData("Btn_CreateWorksetsFromLinkedFiles_V05", "Create Worksets FromLinked Files v05", assemblyPath, "Revit26_Plugin.WSA_V05.Commands.CreateWorksetsFromLinkedFilesV05"));
            setup.AddPushButton(new PushButtonData("Btn_CreateWorksetsFromLinkedFiles_V009", "Create Worksets FromLinked Files V 009", assemblyPath, "Revit26_Plugin.WSFL_009.Commands.CreateWorksetsFromLinkedFiles"));
            //Workset creation-mange-ment tools
            setup.AddPushButton(new PushButtonData("Btn_CreateWorksetsFromLinkedFilesv10", "Create Worksets FromLinked Files v10", assemblyPath, "Revit26_Plugin.WSFL_010.Commands.CreateWorksetsFromLinkedFiles"));
            setup.AddPushButton(new PushButtonData("Btn_WorkSetmanager", "WorkSetmanager", assemblyPath, "WorksetManager_01.Commands.WorksetManagerCommand"));
            setup.AddPushButton(new PushButtonData("Btn_WorksetRenamer_01", "WorksetRenamer_01", assemblyPath, "Revit26_Plugin.WorksetRenamer_01.Command"));

            setup.AddPushButton(new PushButtonData("Btn_WorksetManager_02", "WorksetManager_02", assemblyPath, " Revit26_Plugin.WorksetManager_02.Commands.WorksetManagerCommand"));
            setup.AddPushButton(new PushButtonData("Btn_WorksetManager_03", "WorksetManager_03", assemblyPath, " Revit26_Plugin.WorksetManager_03.Commands.WorksetManagerCommand"));
            setup.AddPushButton(new PushButtonData("Btn_WorksetManager_05", "WorksetManager_05", assemblyPath, " Revit26_Plugin.WorksetManager_05.Commands.WorksetManagerCommand"));
            setup.AddPushButton(new PushButtonData("Btn_WorksetManager_06", "WorksetManager_06", assemblyPath, " Revit26_Plugin.WorksetManager.V06.Commands.WorksetManagerCommand"));

            PulldownButton Linker = panel.AddItem(new PulldownButtonData("Batch Link", "Batch Link")) as PulldownButton;
            Linker.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Linker_32.png");

            Linker.AddPushButton(new PushButtonData("BatchLinkDwgCommand", "BatchLinkDwgCommand", assemblyPath, "BatchDwgFamilyLinker.Command.BatchLinkDwgCommand"));
            Linker.AddPushButton(new PushButtonData("DwgSymbolicConverter_V01", "DwgSymbolicConverter_V01", assemblyPath, "Revit26_Plugin.DwgSymbolicConverter_V01.Commands.LaunchCommand"));
            Linker.AddPushButton(new PushButtonData("DwgSymbolicConverter_V02", "DwgSymbolicConverter_V02", assemblyPath, "Revit26_Plugin.DwgSymbolicConverter_V02.Commands.LaunchCommand"));
            Linker.AddPushButton(new PushButtonData("DwgSymbolicConverter_V03", "DwgSymbolicConverter_V03", assemblyPath, "Revit26_Plugin.DwgSymbolicConverter_V03.Commands.LaunchCommand"));
        }
    }
}
