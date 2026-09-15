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

            // Two coexisting implementations of the same tool — one split
            // button instead of two stacked entries: one click runs VA006
            // (the newer build), the dropdown arrow reveals VA003.
            var linesVA006 = new PushButtonData("Btn_ DeatailLInes VA006", "From Links (VA006)", assemblyPath, "Revit26_Plugin.LinkedDetailLineGenerator.VA006.Commands.OpenLinkedDetailLineGeneratorCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.Linematch32_16.png"),
                ToolTip = "Create Detail Lines From Linked Files (VA006)"
            };
            var linesVA003 = new PushButtonData("Btn_ DeatailLInes VA003", "From Links (VA003)", assemblyPath, "Revit26_Plugin.LinkedDetailLineGenerator.VA003.Commands.OpenLinkedDetailLineGeneratorCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.CreateDetailLines_16.png"),
                ToolTip = "Create Detail Lines From Linked Files (VA003)"
            };
            var linesSplitData = RibbonLayoutHelper.CreateSplitButtonData("Split_DetailLinesFromLinks", "From Links", linesVA006);

            var createItems = RibbonLayoutHelper.AddStackedButtons(createPanel, new List<RibbonItemData> { linesSplitData });
            RibbonLayoutHelper.WireSplitButton(createItems, "Split_DetailLinesFromLinks", linesVA006, linesVA003);

            RibbonPanel processPanel = app.CreateRibbonPanel(tabName, "Detail Line Process");
            RibbonLayoutHelper.AddStackedButtons(processPanel, new List<RibbonItemData>
            {
                new PushButtonData("Btn_DetailLineClosedLoop_V001", "Detail Line Closed Loop", assemblyPath, "Revit26_Plugin.DetailLineClosedLoop.V001.Commands.DetailLineClosedLoopCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.ClosedLoop_16.png")
                },
            });
        }
    }
}
