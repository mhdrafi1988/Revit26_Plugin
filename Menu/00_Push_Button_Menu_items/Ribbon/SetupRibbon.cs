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

            // Split button: pick the Family-editor tool or the Project tool — choose wisely,
            // running a Family tool in a Project (or vice versa) will fail the context check.
            SplitButton setupSplit = panel.AddItem(new SplitButtonData("SetupToolsSplit", "Setup Tools")) as SplitButton;
            setupSplit.IsSynchronizedWithCurrentItem = true;
            setupSplit.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.Setting32.png");

            //Family Editor tools
            setupSplit.AddPushButton(new PushButtonData("BatchLinkDwgCommand", "[Family] Batch Link DWG Family", assemblyPath, "BatchDwgFamilyLinker.Command.BatchLinkDwgCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.Linker_32.png")
            });
            setupSplit.AddPushButton(new PushButtonData("Btn_DwgToLines_V005", "[Family] DWG To Lines — V005", assemblyPath, "Revit26_Plugin.DwgToLines.V005.Commands.DwgToLinesCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.DwgToLines_32.png")
            });

            //Project tools (worksets + linked-file DWG import)
            setupSplit.AddPushButton(new PushButtonData("Btn_WorksetManager_11", "[Project] Create Worksets From Linked Files — V011", assemblyPath, "Revit26_Plugin.WSFL.V011.Commands.CreateWorksetsFromLinkedFiles")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.WorksetsFromLinks_32.png")
            });
            setupSplit.AddPushButton(new PushButtonData("Btn_WorksetRenamer_V003", "[Project] Workset Renamer — V003", assemblyPath, "Revit26_Plugin.WorksetRenamer.V003.Command")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.WorksetRename_32.png")
            });
            setupSplit.AddPushButton(new PushButtonData("Btn_WorksetManager_V012_New", "[Project] Workset Manager — V012", assemblyPath, "Revit26_Plugin.WorksetManager.V012.Commands.WorksetManagerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.WorksetManager_32.png")
            });
            setupSplit.AddPushButton(new PushButtonData("Btn_DwgToDetailLines_V002", "[Project] DWG To Detail Lines — V002", assemblyPath, "Revit26_Plugin.DwgToDetailLines.V002.Commands.LaunchCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.DwgToDetailLines_32.png")
            });
            setupSplit.AddPushButton(new PushButtonData("Btn_DwgToDetailLines_V011", "[Project] DWG To Detail Lines — V011", assemblyPath, "Revit26_Plugin.DwgToDetailLines.V011.Commands.DwgToDetailLinesCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.DwgToDetailLines_V011_32.png")
            });
        }
    }
}
