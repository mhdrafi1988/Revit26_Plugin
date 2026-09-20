using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
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
            RibbonLayoutHelper.AddStackedButtons(familyPanel, new List<RibbonItemData>
            {
                // Batch Link DWG has no version number anywhere in its source
                // (folder is just "BatchDwgFamilyLinker_WOrking"), so the tip
                // says so rather than inventing one.
                new PushButtonData("BatchLinkDwgCommand", "Batch Link DWG", assemblyPath, "BatchDwgFamilyLinker.Command.BatchLinkDwgCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.Linker_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Batch Link DWG Family", "Working build")
                },
                new PushButtonData("Btn_DwgToLines_V005", "DWG To Lines", assemblyPath, "Revit26_Plugin.DwgToLines.V005.Commands.DwgToLinesCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.DwgToLines_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("DWG To Lines", "V005")
                },
            });

            RibbonPanel projectPanel = app.CreateRibbonPanel(tabName, "Project Tools");

            // Two coexisting implementations of the same tool, collected under
            // one pulldown button — each version is a push button inside the dropdown.
            var dwgLinesV011 = new PushButtonData("Btn_DwgToDetailLines_V011", "Detail Lines V011", assemblyPath, "Revit26_Plugin.DwgToDetailLines.V011.Commands.DwgToDetailLinesCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.DwgToDetailLines_V011_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("DWG To Detail Lines", "V011")
            };
            var dwgLinesV002 = new PushButtonData("Btn_DwgToDetailLines_V002", "Detail Lines V002", assemblyPath, "Revit26_Plugin.DwgToDetailLines.V002.Commands.LaunchCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.DwgToDetailLines_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("DWG To Detail Lines", "V002")
            };
            var dwgLinesPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_DwgToDetailLines", "Detail Lines", dwgLinesV011);

            var worksetsFromLinks = new PushButtonData("Btn_WorksetManager_11", "Worksets From Links", assemblyPath, "Revit26_Plugin.WSFL.V011.Commands.CreateWorksetsFromLinkedFiles")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.WorksetsFromLinks_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Create Worksets From Linked Files", "V011")
            };
            var worksetManager = new PushButtonData("Btn_WorksetManager_V012_New", "Workset Manager", assemblyPath, "Revit26_Plugin.WorksetManager.V012.Commands.WorksetManagerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.WorksetManager_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Workset Manager", "V012")
            };

            var projectItems = RibbonLayoutHelper.AddStackedButtons(projectPanel, new List<RibbonItemData>
            {
                worksetManager,
                worksetsFromLinks,
                new PushButtonData("Btn_WorksetRenamer_V003", "Workset Renamer", assemblyPath, "Revit26_Plugin.WorksetRenamer.V003.Command")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.WorksetRename_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Workset Renamer", "V003")
                },
                dwgLinesPulldownData,
            });
            RibbonLayoutHelper.WirePulldownButton(projectItems, "Pulldown_DwgToDetailLines", dwgLinesV011, dwgLinesV002);
        }
    }
}
