using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
using System.Collections.Generic;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class SheetToolsRibbon
    {
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel createPanel = app.CreateRibbonPanel(tabName, "Sheet Create");
            RibbonLayoutHelper.AddStackedButtons(createPanel, new List<PushButtonData>
            {
                // Two coexisting implementations of the same tool — version
                // kept only here so the pair stays distinguishable.
                new PushButtonData("Btn_PlanFromScopeBox.V003", "Scope Box (V003)", assemblyPath, "Revit26_Plugin.PlanFromScopeBox.V003.Commands.PlanFromScopeBoxCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.PlanFromScopeBox_16.png"),
                    ToolTip = "Plan From Scope Box (V003)"
                },
                new PushButtonData("Btn_PlanFromScopeBox.V004", "Scope Box (V004)", assemblyPath, "Revit26_Plugin.PlanFromScopeBox.V004.Commands.PlanFromScopeBoxCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.PlanFromScopeBox_V004_16.png"),
                    ToolTip = "Plan From Scope Box (V004)"
                },
                new PushButtonData("Btn_SmartViewToSheetPlacer.V221", "Sheet Placer (V221)", assemblyPath, "Revit26_Plugin.SmartViewToSheetPlacer.V221.SmartViewToSheetPlacerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SmartViewToSheetPlacer_16.png"),
                    ToolTip = "Smart View To Sheet Placer (V221)"
                },
                new PushButtonData("Btn_SmartViewToSheetPlacer.V222", "Smart View To Sheet Placer (V222)", assemblyPath, "Revit26_Plugin.SmartViewToSheetPlacer.V222.SmartViewToSheetPlacerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SmartViewToSheetPlacer_V222_16.png")
                },
            });

            RibbonPanel placePanel = app.CreateRibbonPanel(tabName, "Sheet Place");
            RibbonLayoutHelper.AddStackedButtons(placePanel, new List<PushButtonData>
            {
                new PushButtonData("Btn_SheetAutoRearrange.V024", "Rearrange (V024)", assemblyPath, "Revit26_Plugin.SheetAutoRearrange.V024.Commands.SheetAutoRearrangeCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SheetAutoRearrange_16.png"),
                    ToolTip = "Sheet Auto Rearrange (V024)"
                },
                new PushButtonData("Btn_SheetAutoRearrange.V025", "Rearrange (V025)", assemblyPath, "Revit26_Plugin.SheetAutoRearrange.V025.Commands.SheetAutoRearrangeCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SheetAutoRearrange_V025_16.png"),
                    ToolTip = "Sheet Auto Rearrange (V025)"
                },
            });
        }
    }
}
