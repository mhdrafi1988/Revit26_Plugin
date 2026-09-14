using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
using System.Collections.Generic;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class DetailLInesRibbon
    {
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = app.CreateRibbonPanel(tabName, "Detail Lines");

            var buttons = new List<PushButtonData>
            {
                new PushButtonData("Btn_ DeatailLInes VA003", "Create Detail Lines From Linked Files — VA003", assemblyPath, "Revit26_Plugin.LinkedDetailLineGenerator.VA003.Commands.OpenLinkedDetailLineGeneratorCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.CreateDetailLines.png")
                },
                new PushButtonData("Btn_ DeatailLInes VA006", "Create Detail Lines From Linked Files — VA006", assemblyPath, "Revit26_Plugin.LinkedDetailLineGenerator.VA006.Commands.OpenLinkedDetailLineGeneratorCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.Linematch32.png")
                },
                new PushButtonData("Btn_DetailLineClosedLoop_V001", "Detail Line Closed Loop — V001", assemblyPath, "Revit26_Plugin.DetailLineClosedLoop.V001.Commands.DetailLineClosedLoopCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DetailLiner.ClosedLoop_32.png")
                },
            };

            RibbonLayoutHelper.AddStackedButtons(panel, buttons);
        }
    }
}
