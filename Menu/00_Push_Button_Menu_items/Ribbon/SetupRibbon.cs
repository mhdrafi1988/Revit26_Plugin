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

            //Workset creation/management tools
            setup.AddPushButton(new PushButtonData("Btn_WorksetManager_11", "Create Worksets From Linked Files — V011", assemblyPath, "Revit26_Plugin.WSFL.V011.Commands.CreateWorksetsFromLinkedFiles"));
            setup.AddPushButton(new PushButtonData("Btn_WorksetRenamer_V003", "Workset Renamer — V003", assemblyPath, "Revit26_Plugin.WorksetRenamer.V003.Command"));
            setup.AddPushButton(new PushButtonData("Btn_WorksetManager_V012_New", "Workset Manager — V012", assemblyPath, "Revit26_Plugin.WorksetManager.V012.Commands.WorksetManagerCommand"));

            PulldownButton Linker = panel.AddItem(new PulldownButtonData("Batch Link", "Batch Link")) as PulldownButton;
            Linker.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.Linker_32.png");

            Linker.AddPushButton(new PushButtonData("BatchLinkDwgCommand", "Batch Link DWG Family", assemblyPath, "BatchDwgFamilyLinker.Command.BatchLinkDwgCommand"));
            Linker.AddPushButton(new PushButtonData("DwgSymbolicConverter_V01", "DWG Symbolic Converter — V01", assemblyPath, "Revit26_Plugin.DwgSymbolicConverter_V01.Commands.LaunchCommand"));
            Linker.AddPushButton(new PushButtonData("DwgSymbolicConverter_V03", "DWG Symbolic Converter — V03", assemblyPath, "Revit26_Plugin.DwgSymbolicConverter_V03.Commands.LaunchCommand"));
            Linker.AddPushButton(new PushButtonData("Btn_DwgToLines_V005", "DWG To Lines — V005", assemblyPath, "Revit26_Plugin.DwgToLines.V005.Commands.DwgToLinesCommand"));
            Linker.AddPushButton(new PushButtonData("Btn_DwgToDetailLines_V011", "DWG To Detail Lines — V011", assemblyPath, "Revit26_Plugin.DwgToDetailLines.V011.Commands.DwgToDetailLinesCommand"));
        }
    }
}
