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
            // UI Standard version second, the ToolWindowShell version (VA009) third.
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
            var linesVA009 = new PushButtonData("Btn_ DeatailLInes VA009", "From Links VA009", assemblyPath, "Revit26_Plugin.LinkedDetailLineGenerator.VA009.Commands.OpenLinkedDetailLineGeneratorCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.Linematch32_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Detail Lines From Links", "VA009",
                    "VA008 on the shared ToolWindowShell (log in body, element-selection lists scroll with the body).")
            };
            var linesPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_DetailLinesFromLinks", "From Links", linesVA007);

            var createItems = RibbonLayoutHelper.AddStackedButtons(createPanel, new List<RibbonItemData> { linesPulldownData });
            RibbonLayoutHelper.WirePulldownButton(createItems, "Pulldown_DetailLinesFromLinks", linesVA007, linesVA008, linesVA009, linesVA006);

            RibbonPanel processPanel = app.CreateRibbonPanel(tabName, "Detail Line Process");

            var closedLoopV002 = new PushButtonData("Btn_DetailLineClosedLoop_V002", "Closed Loop V002", assemblyPath, "Revit26_Plugin.DetailLineClosedLoop.V002.Commands.DetailLineClosedLoopCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.ClosedLoop_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Detail Line Closed Loop", "V002",
                    "UI Standard layout: two-column middle with a full-height Created Lines grid, footer log, Run → Close.")
            };
            var closedLoopV003 = new PushButtonData("Btn_DetailLineClosedLoop_V003", "Closed Loop V003", assemblyPath, "Revit26_Plugin.DetailLineClosedLoop.V003.Commands.DetailLineClosedLoopCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.ClosedLoop_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Detail Line Closed Loop", "V003",
                    "V002 on the shared ToolWindowShell (log in body, capped grid).")
            };
            var closedLoopPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_DetailLineClosedLoop", "Detail Line Closed Loop", closedLoopV002);

            var processItems = RibbonLayoutHelper.AddStackedButtons(processPanel, new List<RibbonItemData> { closedLoopPulldownData });
            RibbonLayoutHelper.WirePulldownButton(processItems, "Pulldown_DetailLineClosedLoop", closedLoopV002, closedLoopV003);
        }
    }
}
