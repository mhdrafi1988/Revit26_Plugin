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

            // Newest version first: the ToolWindowShell version, then the previous one.
            var dtlLineV009 = new PushButtonData("Btn_DtlLine_09", "Detail Lines V009", assemblyPath, "Revit26_Plugin.DtlLineDim.V009.Commands.DtlLineDimCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Dimensions.AutoDimDetailLine_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Auto Dim Detail Line", "V009",
                    "UI Standard layout: metrics card, always-visible footer log with Copy All / Copy Selected, Generate Dimensions → Close.")
            };
            var dtlLineV010 = new PushButtonData("Btn_DtlLine_10", "Detail Lines V010", assemblyPath, "Revit26_Plugin.DtlLineDim.V010.Commands.DtlLineDimCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Dimensions.AutoDimDetailLine_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Auto Dim Detail Line", "V010",
                    "V009 on the shared ToolWindowShell (log in body).")
            };
            var dtlLinePulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_DtlLineDim", "Detail Lines", dtlLineV009);

            var items = RibbonLayoutHelper.AddStackedButtons(panel, new List<RibbonItemData> { dtlLinePulldownData });
            RibbonLayoutHelper.WirePulldownButton(items, "Pulldown_DtlLineDim", dtlLineV010, dtlLineV009);
        }
    }
}
