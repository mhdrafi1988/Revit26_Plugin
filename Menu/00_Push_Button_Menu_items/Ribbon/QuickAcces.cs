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

            PushButton auto_slope_button = panel.AddItem(new PushButtonData("Btn_AutoSlopeByPoint_05", "Auto Slope Point_05_QA", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint_05.Commands.AutoSlopeCommand")) as PushButton;
            auto_slope_button.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.AutoslopeV2_32.png");

            PushButton auto_slope_V5_button = panel.AddItem(new PushButtonData("Btn_AutoSlopeByPoint_0q5", "Auto Slope Poqint Riqdge", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint.WithRidge.Commands.AutoSlopeCommand")) as PushButton;
            auto_slope_V5_button.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.AutoslopeV5_32.png");

            PushButton auto_slope_V6_button = panel.AddItem(new PushButtonData("Btn_RoofRidgeLines_Q53", "Auto Ridge Creator 01(53)", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V53.Commands.RoofRidgeCommand")) as PushButton;
            auto_slope_V6_button.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.AutoslopeV6_64.png");
        }
    }
}