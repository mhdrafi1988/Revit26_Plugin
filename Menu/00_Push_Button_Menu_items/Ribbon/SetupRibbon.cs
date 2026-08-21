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
            setup.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.Setting32.png");

            //Workset creation-mange-ment tools
            setup.AddPushButton(new PushButtonData("Btn_WorksetManager_11", "WorksetManager_ v11", assemblyPath, "Revit26_Plugin.WSFL.V011.Commands.CreateWorksetsFromLinkedFiles"));
            setup.AddPushButton(new PushButtonData("Btn_WorksetManager_010", "WorksetManager_ 010", assemblyPath, "Revit26_Plugin.WorksetManager.V010.CreateWorksetsFromLinkedFiles"));
            setup.AddPushButton(new PushButtonData("Btn_WorksetManager_011", "WorksetManager_ 011", assemblyPath, "Revit26_Plugin.WorksetManager.V011.CreateWorksetsFromLinkedFiles"));
            setup.AddPushButton(new PushButtonData("Btn_WorksetRenamer_V003", "Workset Renamer V003", assemblyPath, "Revit26_Plugin.WorksetRenamer.V003.Command"));
            setup.AddPushButton(new PushButtonData("Btn_WorksetManager_V012_New", "Workset Manager   V012 (New)", assemblyPath, "Revit26_Plugin.WorksetManager.V012.Commands.WorksetManagerCommand"));//Refactored

            PulldownButton Linker = panel.AddItem(new PulldownButtonData("Batch Link", "Batch Link")) as PulldownButton;
            Linker.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.Linker_32.png");

            Linker.AddPushButton(new PushButtonData("BatchLinkDwgCommand", "BatchLinkDwgCommand", assemblyPath, "BatchDwgFamilyLinker.Command.BatchLinkDwgCommand"));
            Linker.AddPushButton(new PushButtonData("DwgSymbolicConverter_V01", "DwgSymbolicConverter_V01", assemblyPath, "Revit26_Plugin.DwgSymbolicConverter_V01.Commands.LaunchCommand"));
            Linker.AddPushButton(new PushButtonData("DwgSymbolicConverter_V03", "DwgSymbolicConverter_V03", assemblyPath, "Revit26_Plugin.DwgSymbolicConverter_V03.Commands.LaunchCommand"));
            Linker.AddPushButton(new PushButtonData("DwgSymbolicConverter_V04", "DwgSymbolicConverter_V04", assemblyPath, "Revit26_Plugin.DwgToLines.V004.Commands.LaunchCommand"));

            Linker.AddPushButton(new PushButtonData("DwgToDetailLines_Project_V001", "Dwg To Detail Lines Project V001", assemblyPath, "Revit26_Plugin.DwgToDetailLines.Project.V001.Commands.LaunchCommand"));
            Linker.AddPushButton(new PushButtonData("DwgToDetailLines_Project_V010", "Dwg To Detail Lines Project V010", assemblyPath, "Revit26_Plugin.DwgToDetailLines.V010.Commands.LaunchCommand"));
            Linker.AddPushButton(new PushButtonData("Btn_DwgToDetailLines_V002", "Dwg To Detail Lines V002", assemblyPath, "Revit26_Plugin.DwgToDetailLines.V002.Commands.LaunchCommand"));
            Linker.AddPushButton(new PushButtonData("Btn_DwgToLines_V005_New", "DWG to Lines   V005 (New)", assemblyPath, "Revit26_Plugin.DwgToLines.V005.Commands.DwgToLinesCommand"));//Refactored
            Linker.AddPushButton(new PushButtonData("Btn_DwgToDetailLines_V011_New", "DWG to Detail Lines   V011 (New)", assemblyPath, "Revit26_Plugin.DwgToDetailLines.V011.Commands.DwgToDetailLinesCommand"));//Refactored
        }
    }
}
