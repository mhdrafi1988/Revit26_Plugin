using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
using System.Collections.Generic;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class DetailLInesRibbon
    {
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel createPanel = app.CreateRibbonPanel(tabName, "Detail Line Create");

            // Only the newest version is kept; it stays in a pulldown so a
            // future version can be added beside it.
            var linesVA007 = new PushButtonData("Btn_ DeatailLInes VA007", "From Links VA007", assemblyPath, "Revit26_Plugin.LinkedDetailLineGenerator.VA007.Commands.OpenLinkedDetailLineGeneratorCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.Linematch32_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Detail Lines From Links", "VA007",
                    "Create Detail Lines From Linked Files, clipped to a pre-selected Floor/Roof boundary")
            };
            var linesPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_DetailLinesFromLinks", "From Links", linesVA007);

            var createItems = RibbonLayoutHelper.AddStackedButtons(createPanel, new List<RibbonItemData> { linesPulldownData });
            RibbonLayoutHelper.WirePulldownButton(createItems, "Pulldown_DetailLinesFromLinks", linesVA007);

            RibbonPanel processPanel = app.CreateRibbonPanel(tabName, "Detail Line Process");
            RibbonLayoutHelper.AddStackedButtons(processPanel, new List<RibbonItemData>
            {
                new PushButtonData("Btn_DetailLineClosedLoop_V001", "Detail Line Closed Loop", assemblyPath, "Revit26_Plugin.DetailLineClosedLoop.V001.Commands.DetailLineClosedLoopCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.ClosedLoop_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Detail Line Closed Loop", "V001")
                },
            });
        }
    }
}
