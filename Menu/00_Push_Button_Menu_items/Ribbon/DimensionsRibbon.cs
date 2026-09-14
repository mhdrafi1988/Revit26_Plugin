using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
using System.Collections.Generic;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class DimensionsRibbon
    {
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = app.CreateRibbonPanel(tabName, "Dimensions");

            var buttons = new List<PushButtonData>
            {
                new PushButtonData("Btn_DtlLine_08", "Auto Dim Detail Line — V008", assemblyPath, "Revit26_Plugin.DtlLineDim.V008.Commands.DtlLineDimCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Dimensions.AutoDimDetailLine_32.png")
                },
            };

            RibbonLayoutHelper.AddStackedButtons(panel, buttons);
        }
    }
}
