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
            setup.AddPushButton(new PushButtonData("Btn_WorksetManager_11", "WorksetManager_ v11", assemblyPath, "Revit26_Plugin.WSFL.V011.Commands.CreateWorksetsFromLinkedFiles"));
            setup.AddPushButton(new PushButtonData("Btn_WorksetManager_007", "WorksetManager_ 007", assemblyPath, "Revit26_Plugin.WorksetManager_007.CreateWorksetsFromLinkedFilesCommand"));
            setup.AddPushButton(new PushButtonData("Btn_WorksetManager_009", "WorksetManager_ 009", assemblyPath, "Revit26_Plugin.WorksetManager.V009.CreateWorksetsFromLinkedFiles"));
            setup.AddPushButton(new PushButtonData("Btn_WorksetManager_010", "WorksetManager_ 010", assemblyPath, "Revit26_Plugin.WorksetManager.V010.CreateWorksetsFromLinkedFiles"));
            setup.AddPushButton(new PushButtonData("Btn_WorksetManager_011", "WorksetManager_ 011", assemblyPath, "Revit26_Plugin.WorksetManager.V011.CreateWorksetsFromLinkedFiles"));
            setup.AddPushButton(new PushButtonData("Btn_WorksetRenamer_V003", "Workset Renamer V003", assemblyPath, "Revit26_Plugin.WorksetRenamer.V003.Command"));

            PulldownButton Linker = panel.AddItem(new PulldownButtonData("Batch Link", "Batch Link")) as PulldownButton;
            Linker.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Linker_32.png");

            Linker.AddPushButton(new PushButtonData("BatchLinkDwgCommand", "BatchLinkDwgCommand", assemblyPath, "BatchDwgFamilyLinker.Command.BatchLinkDwgCommand"));
            Linker.AddPushButton(new PushButtonData("DwgSymbolicConverter_V01", "DwgSymbolicConverter_V01", assemblyPath, "Revit26_Plugin.DwgSymbolicConverter_V01.Commands.LaunchCommand"));
            Linker.AddPushButton(new PushButtonData("DwgSymbolicConverter_V02", "DwgSymbolicConverter_V02", assemblyPath, "Revit26_Plugin.DwgSymbolicConverter_V02.Commands.LaunchCommand"));
            Linker.AddPushButton(new PushButtonData("DwgSymbolicConverter_V03", "DwgSymbolicConverter_V03", assemblyPath, "Revit26_Plugin.DwgSymbolicConverter_V03.Commands.LaunchCommand"));
            Linker.AddPushButton(new PushButtonData("DwgSymbolicConverter_V04", "DwgSymbolicConverter_V04", assemblyPath, "Revit26_Plugin.DwgToLines.V004.Commands.LaunchCommand"));

            Linker.AddPushButton(new PushButtonData("DwgToDetailLines_Project_V006", "Dwg To Detail Lines Project V006", assemblyPath, "Revit26_Plugin.DwgToDetailLines.Project.V006.Commands.LaunchCommand"));
            Linker.AddPushButton(new PushButtonData("DwgToDetailLines_Project_V007", "Dwg To Detail Lines Project V007", assemblyPath, "Revit26_Plugin.DwgToDetailLines.Project.V007.Commands.LaunchCommand"));
            Linker.AddPushButton(new PushButtonData("DwgToDetailLines_Project_V009", "Dwg To Detail Lines Project V009", assemblyPath, "Revit26_Plugin.DwgToDetailLines.Project.V009.Commands.LaunchCommand"));
            Linker.AddPushButton(new PushButtonData("DwgToDetailLines_Project_V010", "Dwg To Detail Lines Project V010", assemblyPath, "Revit26_Plugin.DwgToDetailLines.V010.Commands.LaunchCommand"));
            Linker.AddPushButton(new PushButtonData("Btn_DwgToDetailLines_V002", "Dwg To Detail Lines V002", assemblyPath, "Revit26_Plugin.DwgToDetailLines.V002.Commands.LaunchCommand"));
        }
    }
}
