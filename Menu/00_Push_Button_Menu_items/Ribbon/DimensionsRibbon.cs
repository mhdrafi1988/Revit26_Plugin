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

            // Previous version first, the UI Standard version second.
            var dtlLineV008 = new PushButtonData("Btn_DtlLine_08", "Detail Lines V008", assemblyPath, "Revit26_Plugin.DtlLineDim.V008.Commands.DtlLineDimCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Dimensions.AutoDimDetailLine_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Auto Dim Detail Line", "V008")
            };
            var dtlLineV009 = new PushButtonData("Btn_DtlLine_09", "Detail Lines V009", assemblyPath, "Revit26_Plugin.DtlLineDim.V009.Commands.DtlLineDimCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Dimensions.AutoDimDetailLine_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Auto Dim Detail Line", "V009",
                    "UI Standard layout: metrics card, always-visible footer log with Copy All / Copy Selected, Generate Dimensions → Close.")
            };
            var dtlLinePulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_DtlLineDim", "Detail Lines", dtlLineV008);

            var items = RibbonLayoutHelper.AddStackedButtons(panel, new List<RibbonItemData> { dtlLinePulldownData });
            RibbonLayoutHelper.WirePulldownButton(items, "Pulldown_DtlLineDim", dtlLineV008, dtlLineV009);
        }
    }
}
