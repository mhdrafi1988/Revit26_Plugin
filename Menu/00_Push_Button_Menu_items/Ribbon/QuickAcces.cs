using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
using System;
using System.Reflection;
using System.Windows.Markup;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class QuickAccessRibbon
    {

        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = app.CreateRibbonPanel(tabName, "QuickAcces");

            PushButton auto_slope_button = panel.AddItem(new PushButtonData("Btn_AutoSlopeByPoint_04", "Auto Slope Poiqnt", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint_04.Commands.AutoSlopeCommand")) as PushButton;
            auto_slope_button.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.AutoslopeV2_32.png");
            PushButton auto_slope_V5_button = panel.AddItem(new PushButtonData("Btn_AutoSlopeByPoint_0q5", "Auto Slope Poqint Riqdge", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint.WithRidge.Commands.AutoSlopeCommand")) as PushButton;
            auto_slope_V5_button.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.AutoslopeV5_32.png");
            PushButton auto_slope_V6_button = panel.AddItem(new PushButtonData("Btn_RoofRidgeLines_Q51", "Ridge RoofRidgeLqines_Vq51", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V51.Commands.RoofRidgeCommand")) as PushButton;
            auto_slope_V6_button.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.AutoslopeV6_32.png");
        }
    }
}