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

            //Family Editor tools
            setupSplit.AddPushButton(new PushButtonData("BatchLinkDwgCommand", "[Family] Batch Link DWG Family", assemblyPath, "BatchDwgFamilyLinker.Command.BatchLinkDwgCommand"));
            setupSplit.AddPushButton(new PushButtonData("DwgSymbolicConverter_V01", "[Family] DWG Symbolic Converter — V01", assemblyPath, "Revit26_Plugin.DwgSymbolicConverter_V01.Commands.LaunchCommand"));
            setupSplit.AddPushButton(new PushButtonData("DwgSymbolicConverter_V03", "[Family] DWG Symbolic Converter — V03", assemblyPath, "Revit26_Plugin.DwgSymbolicConverter_V03.Commands.LaunchCommand"));
            setupSplit.AddPushButton(new PushButtonData("Btn_DwgToLines_V005", "[Family] DWG To Lines — V005", assemblyPath, "Revit26_Plugin.DwgToLines.V005.Commands.DwgToLinesCommand"));

            //Project tools (worksets + linked-file DWG import)
            setupSplit.AddPushButton(new PushButtonData("Btn_WorksetManager_11", "[Project] Create Worksets From Linked Files — V011", assemblyPath, "Revit26_Plugin.WSFL.V011.Commands.CreateWorksetsFromLinkedFiles"));
            setupSplit.AddPushButton(new PushButtonData("Btn_WorksetRenamer_V003", "[Project] Workset Renamer — V003", assemblyPath, "Revit26_Plugin.WorksetRenamer.V003.Command"));
            setupSplit.AddPushButton(new PushButtonData("Btn_WorksetManager_V012_New", "[Project] Workset Manager — V012", assemblyPath, "Revit26_Plugin.WorksetManager.V012.Commands.WorksetManagerCommand"));
            setupSplit.AddPushButton(new PushButtonData("Btn_DwgToDetailLines_V011", "[Project] DWG To Detail Lines — V011", assemblyPath, "Revit26_Plugin.DwgToDetailLines.V011.Commands.DwgToDetailLinesCommand"));
        }
    }
}
