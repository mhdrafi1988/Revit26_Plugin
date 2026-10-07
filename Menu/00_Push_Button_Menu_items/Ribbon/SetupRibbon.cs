using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
using Revit26_Plugin.Shared.Services;
using System.Collections.Generic;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class SetupRibbon
    {
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            // Family-editor tools and Project tools are split into their own
            // panels — running a Family tool in a Project (or vice versa)
            // fails the context check, so the panel title now makes that clear.
            RibbonPanel familyPanel = app.CreateRibbonPanel(tabName, "Family Tools");

            // Every tool is a pulldown: previous version first, the UI Standard
            // version second.
            // Batch Link DWG's original build has no version number anywhere in
            // its source (folder "BatchDwgFamilyLinker_WOrking"), so the tip says
            // so rather than inventing one; the UI Standard rebuild is V002.
            var batchLinkWorking = new PushButtonData("BatchLinkDwgCommand", "Batch Link DWG (Working)", assemblyPath, "BatchDwgFamilyLinker.Command.BatchLinkDwgCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.Linker_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Batch Link DWG Family", "Working build")
            };
            var batchLinkV002 = new PushButtonData("Btn_BatchLinkDwg_V002", "Batch Link DWG V002", assemblyPath, "BatchDwgFamilyLinker.V002.Command.BatchLinkDwgCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.Linker_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Batch Link DWG Family", "V002", "UI Standard layout: navy theme, metrics card, footer progress + live log with Copy All / Copy Selected, Start Batch → Close.")
            };
            var batchLinkPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_BatchLinkDwg", "Batch Link DWG", batchLinkWorking);

            var dwgToLinesV006 = new PushButtonData("Btn_DwgToLines_V006", "DWG To Lines V006", assemblyPath, "Revit26_Plugin.DwgToLines.V006.Commands.DwgToLinesCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.DwgToLines_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("DWG To Lines", "V006", "UI Standard layout: metrics card, always-visible footer log, Convert DWG → Close.")
            };
            var dwgToLinesV007 = new PushButtonData("Btn_DwgToLines_V007", "DWG To Lines V007", assemblyPath, "Revit26_Plugin.DwgToLines.V007.Commands.DwgToLinesCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.DwgToLines_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("DWG To Lines", "V007", "V006 on the shared ToolWindowShell (log in body).")
            };
            var dwgToLinesPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_DwgToLines", "DWG To Lines", dwgToLinesV006);

            var familyItems = RibbonLayoutHelper.AddStackedButtons(familyPanel, new List<RibbonItemData>
            {
                batchLinkPulldownData,
                dwgToLinesPulldownData,
            });
            RibbonLayoutHelper.WirePulldownButton(familyItems, "Pulldown_BatchLinkDwg", batchLinkWorking, batchLinkV002);
            RibbonLayoutHelper.WirePulldownButton(familyItems, "Pulldown_DwgToLines", dwgToLinesV006, dwgToLinesV007);

            RibbonPanel projectPanel = app.CreateRibbonPanel(tabName, "Project Tools");

            // Two coexisting implementations of the same tool, collected under
            // one pulldown button — each version is a push button inside the dropdown.
            var dwgLinesV011 = new PushButtonData("Btn_DwgToDetailLines_V011", "Detail Lines V011", assemblyPath, "Revit26_Plugin.DwgToDetailLines.V011.Commands.DwgToDetailLinesCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.DwgToDetailLines_V011_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("DWG To Detail Lines", "V011")
            };
            var dwgLinesV012 = new PushButtonData("Btn_DwgToDetailLines_V012", "Detail Lines V012", assemblyPath, "Revit26_Plugin.DwgToDetailLines.V012.Commands.DwgToDetailLinesCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.DwgToDetailLines_V011_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("DWG To Detail Lines", "V012", "UI Standard layout: metrics on top, full-height layer grid, always-visible footer log, Convert DWG → Close.")
            };
            var dwgLinesV013 = new PushButtonData("Btn_DwgToDetailLines_V013", "Detail Lines V013", assemblyPath, "Revit26_Plugin.DwgToDetailLines.V013.Commands.DwgToDetailLinesCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.DwgToDetailLines_V011_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("DWG To Detail Lines", "V013", "V012 on the shared ToolWindowShell (log in body, capped grid).")
            };
            var dwgLinesV014 = new PushButtonData("Btn_DwgToDetailLines_V014", "Detail Lines V014", assemblyPath, "Revit26_Plugin.DwgToDetailLines.V014.Commands.DwgToDetailLinesCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.DwgToDetailLines_V011_16.png"),
                ToolTip = ToolCatalog.DwgToDetailLines.Tip("V013 plus: line style shortlist with auto-assign by CAD colour / lineweight / pattern, and Place beside CAD (offset right by 1× CAD width).")
            };
            var dwgLinesPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_DwgToDetailLines", "Detail Lines", dwgLinesV011);

            var exportDwgV001 = new PushButtonData("Btn_ExportDwgToFolder_V001", "Export DWG V001", assemblyPath, "Revit26_Plugin.ExportDwgToFolder.V001.Commands.ExportDwgToFolderCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.DwgToDetailLines_V011_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Export DWG to Folder", "V001", "Export all embedded CAD links and imports to a folder, organised by source type and view category.")
            };
            var exportDwgPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_ExportDwgToFolder", "Export DWG", exportDwgV001);

            var projectItems = RibbonLayoutHelper.AddStackedButtons(projectPanel, new List<RibbonItemData>
            {
                dwgLinesPulldownData,
                exportDwgPulldownData,
            });
            RibbonLayoutHelper.WirePulldownButton(projectItems, "Pulldown_DwgToDetailLines", dwgLinesV011, dwgLinesV012, dwgLinesV013, dwgLinesV014);
            RibbonLayoutHelper.WirePulldownButton(projectItems, "Pulldown_ExportDwgToFolder", exportDwgV001);
        }
    }
}
