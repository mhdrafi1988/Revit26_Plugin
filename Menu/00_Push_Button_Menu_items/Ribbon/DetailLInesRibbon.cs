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

            // Coexisting versions share one pulldown: previous version first, the
            // UI Standard version second.
            var linesVA007 = new PushButtonData("Btn_ DeatailLInes VA007", "From Links VA007", assemblyPath, "Revit26_Plugin.LinkedDetailLineGenerator.VA007.Commands.OpenLinkedDetailLineGeneratorCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.Linematch32_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Detail Lines From Links", "VA007",
                    "Create Detail Lines From Linked Files, clipped to a pre-selected Floor/Roof boundary")
            };
            var linesVA008 = new PushButtonData("Btn_ DeatailLInes VA008", "From Links VA008", assemblyPath, "Revit26_Plugin.LinkedDetailLineGenerator.VA008.Commands.OpenLinkedDetailLineGeneratorCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.Linematch32_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Detail Lines From Links", "VA008",
                    "UI Standard layout: metrics fixed on top, always-visible footer log with working Copy All / Copy Selected, Create → Close.")
            };
            var linesVA006 = new PushButtonData("Btn_ DeatailLInes VA006", "From Links VA006", assemblyPath, "Revit26_Plugin.LinkedDetailLineGenerator.VA006.Commands.OpenLinkedDetailLineGeneratorCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.Linematch32_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Detail Lines From Links", "VA006", "Create Detail Lines From Linked Files")
            };
            var linesVA003 = new PushButtonData("Btn_ DeatailLInes VA003", "From Links VA003", assemblyPath, "Revit26_Plugin.LinkedDetailLineGenerator.VA003.Commands.OpenLinkedDetailLineGeneratorCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.Linematch32_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Detail Lines From Links", "VA003", "Create Detail Lines From Linked Files")
            };
            var linesPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_DetailLinesFromLinks", "From Links", linesVA007);

            var createItems = RibbonLayoutHelper.AddStackedButtons(createPanel, new List<RibbonItemData> { linesPulldownData });
            RibbonLayoutHelper.WirePulldownButton(createItems, "Pulldown_DetailLinesFromLinks", linesVA007, linesVA008, linesVA006, linesVA003);

            RibbonPanel processPanel = app.CreateRibbonPanel(tabName, "Detail Line Process");

            var closedLoopV001 = new PushButtonData("Btn_DetailLineClosedLoop_V001", "Closed Loop V001", assemblyPath, "Revit26_Plugin.DetailLineClosedLoop.V001.Commands.DetailLineClosedLoopCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.ClosedLoop_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Detail Line Closed Loop", "V001")
            };
            var closedLoopV002 = new PushButtonData("Btn_DetailLineClosedLoop_V002", "Closed Loop V002", assemblyPath, "Revit26_Plugin.DetailLineClosedLoop.V002.Commands.DetailLineClosedLoopCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.ClosedLoop_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Detail Line Closed Loop", "V002",
                    "UI Standard layout: two-column middle with a full-height Created Lines grid, footer log, Run → Close.")
            };
            var closedLoopPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_DetailLineClosedLoop", "Detail Line Closed Loop", closedLoopV001);

            var processItems = RibbonLayoutHelper.AddStackedButtons(processPanel, new List<RibbonItemData> { closedLoopPulldownData });
            RibbonLayoutHelper.WirePulldownButton(processItems, "Pulldown_DetailLineClosedLoop", closedLoopV001, closedLoopV002);
        }
    }
}
