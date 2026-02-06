using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
//using Revit22_Plugin.Utils;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class DimensionsRibbon
    {
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = app.CreateRibbonPanel(tabName, "Dimensions");
            PulldownButton DimMenu = panel.AddItem(new PulldownButtonData("Dimensions", "Dimensions")) as PulldownButton;
            DimMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.DimMenu_32.png");

            DimMenu.AddPushButton(new PushButtonData("Btn_DtlLineDimCommand_01", "Auto Dim Detail Item Line Based_01 (Working)", assemblyPath, "Revit26_Plugin.DtlLineDim_V03.Commands.DtlLineDimCommand_01"));    

        }
    }
}
