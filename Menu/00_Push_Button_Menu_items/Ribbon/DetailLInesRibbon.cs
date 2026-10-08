using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
using System.Collections.Generic;

namespace Revit26_Plugin.Menu.Ribbon
{
    /// <summary>
    /// Builds the Detail Lines panel: From Links, Closed Loop and Line Dims stacked in one column.
    /// Replaces the one-button Detail Line Create, Detail Line Process and Dimensions panels
    /// (plugin 2.4.0) so Revit has room to show every button name.
    /// </summary>
    public static class DetailLInesRibbon
    {
        /// <summary>Creates the Detail Lines panel on <paramref name="tabName"/>.</summary>
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = app.CreateRibbonPanel(tabName, "Detail Lines");

            // Coexisting versions share one pulldown: highest version number first,
            // every older version below it in descending order.
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
            var linesVA010 = new PushButtonData("Btn_ DeatailLInes VA010", "From Links VA010", assemblyPath, "Revit26_Plugin.LinkedDetailLineGenerator.VA010.Commands.OpenLinkedDetailLineGeneratorCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.Linematch32_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Detail Lines From Links", "VA010",
                    "VA009 + ToolViewModelBase (Options 1/2/3): base log/shell, 3-way partial split, TreeVisibilityFilter service.")
            };
            var linesPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_DetailLinesFromLinks", "From Links", linesVA010);

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
            var closedLoopPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_DetailLineClosedLoop", "Closed Loop", closedLoopV003);

            var lineDimsPulldownData = DimensionsRibbon.CreateDetailLineDimPulldown(assemblyPath, out var lineDimsVersions);

            var items = RibbonLayoutHelper.AddStackedButtons(panel, new List<RibbonItemData> { linesPulldownData, closedLoopPulldownData, lineDimsPulldownData });
            RibbonLayoutHelper.WirePulldownButton(items, "Pulldown_DetailLinesFromLinks", linesVA010, linesVA009, linesVA008, linesVA007, linesVA006);
            RibbonLayoutHelper.WirePulldownButton(items, "Pulldown_DetailLineClosedLoop", closedLoopV003, closedLoopV002);
            RibbonLayoutHelper.WirePulldownButton(items, DimensionsRibbon.DetailLineDimPulldownName, lineDimsVersions);
        }
    }
}
