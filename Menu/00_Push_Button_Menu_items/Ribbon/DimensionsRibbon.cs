using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;

namespace Revit26_Plugin.Menu.Ribbon
{
    /// <summary>
    /// Builds the Auto Dim Detail Line pulldown. Since plugin 2.4.0 it sits in the Detail Lines
    /// panel (see <see cref="DetailLInesRibbon"/>); the old one-button Dimensions panel was removed
    /// to keep the tab narrow enough for Revit to show every button name.
    /// </summary>
    public static class DimensionsRibbon
    {
        /// <summary>Internal name of the pulldown (unchanged since it lived in Dimensions).</summary>
        public const string DetailLineDimPulldownName = "Pulldown_DtlLineDim";

        /// <summary>
        /// Returns the "Line Dims" pulldown data to stack in a panel, and in
        /// <paramref name="versions"/> every version, highest first, for
        /// <see cref="RibbonLayoutHelper.WirePulldownButton"/>.
        /// </summary>
        public static PulldownButtonData CreateDetailLineDimPulldown(string assemblyPath, out PushButtonData[] versions)
        {
            // Highest version number first, every older version below it in descending order.
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
            versions = new[] { dtlLineV010, dtlLineV009 };
            return RibbonLayoutHelper.CreatePulldownButtonData(DetailLineDimPulldownName, "Line Dims", dtlLineV010);
        }
    }
}
