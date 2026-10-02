using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
using Revit26_Plugin.Shared.Services;
using System.Collections.Generic;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class DetailLInesRibbon
    {
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel createPanel = app.CreateRibbonPanel(tabName, "Detail Line Create");

            var linesVA007 = new PushButtonData("Btn_ DeatailLInes VA007", "From Links", assemblyPath, "Revit26_Plugin.LinkedDetailLineGenerator.VA007.Commands.OpenLinkedDetailLineGeneratorCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.Linematch32_16.png"),
                ToolTip = ToolCatalog.LinkedDetailLineGenerator.Tip("Create Detail Lines From Linked Files, clipped to a pre-selected Floor/Roof boundary")
            };
            RibbonLayoutHelper.AddStackedButtons(createPanel, new List<RibbonItemData> { linesVA007 });

            RibbonPanel processPanel = app.CreateRibbonPanel(tabName, "Detail Line Process");
            RibbonLayoutHelper.AddStackedButtons(processPanel, new List<RibbonItemData>
            {
                new PushButtonData("Btn_DetailLineClosedLoop_V001", "Detail Line Closed Loop", assemblyPath, "Revit26_Plugin.DetailLineClosedLoop.V001.Commands.DetailLineClosedLoopCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.ClosedLoop_16.png"),
                    ToolTip = ToolCatalog.DetailLineClosedLoop.Tip()
                },
            });
        }
    }
}
