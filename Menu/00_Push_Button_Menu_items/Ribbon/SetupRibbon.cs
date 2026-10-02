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
            RibbonLayoutHelper.AddStackedButtons(familyPanel, new List<RibbonItemData>
            {
                // Batch Link DWG has no version number anywhere in its source
                // (folder is just "BatchDwgFamilyLinker_WOrking"), so the tip
                // says so rather than inventing one.
                new PushButtonData("BatchLinkDwgCommand", "Batch Link DWG", assemblyPath, "BatchDwgFamilyLinker.Command.BatchLinkDwgCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.Linker_16.png"),
                    ToolTip = ToolCatalog.BatchDwgFamilyLinker.Tip()
                },
                new PushButtonData("Btn_DwgToLines_V005", "DWG To Lines", assemblyPath, "Revit26_Plugin.DwgToLines.V005.Commands.DwgToLinesCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.DwgToLines_16.png"),
                    ToolTip = ToolCatalog.DwgToLines.Tip()
                },
            });

            RibbonPanel projectPanel = app.CreateRibbonPanel(tabName, "Project Tools");

            var dwgLinesV011 = new PushButtonData("Btn_DwgToDetailLines_V011", "DWG To Detail Lines", assemblyPath, "Revit26_Plugin.DwgToDetailLines.V011.Commands.DwgToDetailLinesCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.DwgToDetailLines_V011_16.png"),
                ToolTip = ToolCatalog.DwgToDetailLines.Tip()
            };
            RibbonLayoutHelper.AddStackedButtons(projectPanel, new List<RibbonItemData> { dwgLinesV011 });
        }
    }
}
