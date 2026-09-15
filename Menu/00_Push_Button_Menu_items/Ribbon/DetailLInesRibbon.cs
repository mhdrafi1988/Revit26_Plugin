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
            RibbonLayoutHelper.AddStackedButtons(createPanel, new List<PushButtonData>
            {
                // Two coexisting implementations of the same tool — version kept
                // only here so the pair stays distinguishable.
                new PushButtonData("Btn_ DeatailLInes VA003", "From Links (VA003)", assemblyPath, "Revit26_Plugin.LinkedDetailLineGenerator.VA003.Commands.OpenLinkedDetailLineGeneratorCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.CreateDetailLines.png"),
                    ToolTip = "Create Detail Lines From Linked Files (VA003)"
                },
                new PushButtonData("Btn_ DeatailLInes VA006", "From Links (VA006)", assemblyPath, "Revit26_Plugin.LinkedDetailLineGenerator.VA006.Commands.OpenLinkedDetailLineGeneratorCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.Linematch32.png"),
                    ToolTip = "Create Detail Lines From Linked Files (VA006)"
                },
            });

            RibbonPanel processPanel = app.CreateRibbonPanel(tabName, "Detail Line Process");
            RibbonLayoutHelper.AddStackedButtons(processPanel, new List<PushButtonData>
            {
                new PushButtonData("Btn_DetailLineClosedLoop_V001", "Detail Line Closed Loop", assemblyPath, "Revit26_Plugin.DetailLineClosedLoop.V001.Commands.DetailLineClosedLoopCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.ClosedLoop_32.png")
                },
            });
        }
    }
}
