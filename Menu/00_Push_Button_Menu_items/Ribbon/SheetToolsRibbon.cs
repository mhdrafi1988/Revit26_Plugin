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

            // Two coexisting implementations of the same tool, collected under
            // one pulldown button — newest keeps its icon in the dropdown,
            // the older version is text-only.
            var scopeBoxV004 = new PushButtonData("Btn_PlanFromScopeBox.V004", "Scope Box (V004)", assemblyPath, "Revit26_Plugin.PlanFromScopeBox.V004.Commands.PlanFromScopeBoxCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.PlanFromScopeBox_V004_16.png"),
                ToolTip = "Plan From Scope Box (V004)"
            };
            var scopeBoxV003 = new PushButtonData("Btn_PlanFromScopeBox.V003", "Scope Box (V003)", assemblyPath, "Revit26_Plugin.PlanFromScopeBox.V003.Commands.PlanFromScopeBoxCommand")
            {
                ToolTip = "Plan From Scope Box (V003)"
            };
            var scopeBoxPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_PlanFromScopeBox", "Scope Box", scopeBoxV004);

            var sheetPlacerV222 = new PushButtonData("Btn_SmartViewToSheetPlacer.V222", "View To Sheet (V222)", assemblyPath, "Revit26_Plugin.SmartViewToSheetPlacer.V222.SmartViewToSheetPlacerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SmartViewToSheetPlacer_V222_16.png"),
                ToolTip = "Smart View To Sheet Placer (V222)"
            };
            var sheetPlacerV221 = new PushButtonData("Btn_SmartViewToSheetPlacer.V221", "Sheet Placer (V221)", assemblyPath, "Revit26_Plugin.SmartViewToSheetPlacer.V221.SmartViewToSheetPlacerCommand")
            {
                ToolTip = "Smart View To Sheet Placer (V221)"
            };
            var sheetPlacerPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_SmartViewToSheetPlacer", "Sheet Placer", sheetPlacerV222);

            // Pulldown buttons can share a stack, so both tools now sit
            // together in one 2-item stack instead of two lone buttons.
            var sheetCreateItems = RibbonLayoutHelper.AddStackedButtons(createPanel, new List<RibbonItemData> { scopeBoxPulldownData, sheetPlacerPulldownData });
            RibbonLayoutHelper.WirePulldownButton(sheetCreateItems, "Pulldown_PlanFromScopeBox", scopeBoxV004, scopeBoxV003);
            RibbonLayoutHelper.WirePulldownButton(sheetCreateItems, "Pulldown_SmartViewToSheetPlacer", sheetPlacerV222, sheetPlacerV221);

            RibbonPanel placePanel = app.CreateRibbonPanel(tabName, "Sheet Place");

            var rearrangeV025 = new PushButtonData("Btn_SheetAutoRearrange.V025", "Rearrange (V025)", assemblyPath, "Revit26_Plugin.SheetAutoRearrange.V025.Commands.SheetAutoRearrangeCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SheetAutoRearrange_V025_16.png"),
                ToolTip = "Sheet Auto Rearrange (V025)"
            };
            var rearrangeV024 = new PushButtonData("Btn_SheetAutoRearrange.V024", "Rearrange (V024)", assemblyPath, "Revit26_Plugin.SheetAutoRearrange.V024.Commands.SheetAutoRearrangeCommand")
            {
                ToolTip = "Sheet Auto Rearrange (V024)"
            };
            var rearrangePulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_SheetAutoRearrange", "Rearrange", rearrangeV025);

            var placeItems = RibbonLayoutHelper.AddStackedButtons(placePanel, new List<RibbonItemData> { rearrangePulldownData });
            RibbonLayoutHelper.WirePulldownButton(placeItems, "Pulldown_SheetAutoRearrange", rearrangeV025, rearrangeV024);
        }
    }
}
