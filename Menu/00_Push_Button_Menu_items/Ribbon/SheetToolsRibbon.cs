using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
using System.Collections.Generic;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class SheetToolsRibbon
    {
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = app.CreateRibbonPanel(tabName, "Sheet Tools");

            var buttons = new List<PushButtonData>
            {
                new PushButtonData("Btn_PlanFromScopeBox.V003", "Plan From Scope Box — V003", assemblyPath, "Revit26_Plugin.PlanFromScopeBox.V003.Commands.PlanFromScopeBoxCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.PlanFromScopeBox_32.png")
                },
                new PushButtonData("Btn_PlanFromScopeBox.V004", "Plan From Scope Box — V004", assemblyPath, "Revit26_Plugin.PlanFromScopeBox.V004.Commands.PlanFromScopeBoxCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.PlanFromScopeBox_V004_32.png")
                },
                new PushButtonData("Btn_SmartViewToSheetPlacer.V221", "Smart View To Sheet Placer — V221", assemblyPath, "Revit26_Plugin.SmartViewToSheetPlacer.V221.SmartViewToSheetPlacerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SmartViewToSheetPlacer_32.png")
                },
                new PushButtonData("Btn_SmartViewToSheetPlacer.V222", "Smart View To Sheet Placer — V222", assemblyPath, "Revit26_Plugin.SmartViewToSheetPlacer.V222.SmartViewToSheetPlacerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SmartViewToSheetPlacer_V222_32.png")
                },

                new PushButtonData("Btn_SheetAutoRearrange.V024", "Sheet Auto Rearrange — V024", assemblyPath, "Revit26_Plugin.SheetAutoRearrange.V024.Commands.SheetAutoRearrangeCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SheetAutoRearrange_32.png")
                },
                new PushButtonData("Btn_SheetAutoRearrange.V025", "Sheet Auto Rearrange — V025", assemblyPath, "Revit26_Plugin.SheetAutoRearrange.V025.Commands.SheetAutoRearrangeCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SheetAutoRearrange_V025_32.png")
                },
            };

            RibbonLayoutHelper.AddStackedButtons(panel, buttons);
        }
    }
}
