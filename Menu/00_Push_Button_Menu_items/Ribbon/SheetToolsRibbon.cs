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

            // Two coexisting implementations of the same tool — one split
            // button instead of two stacked entries: one click runs the
            // newer version, the dropdown arrow reveals the older one.
            var scopeBoxV004 = new PushButtonData("Btn_PlanFromScopeBox.V004", "Scope Box (V004)", assemblyPath, "Revit26_Plugin.PlanFromScopeBox.V004.Commands.PlanFromScopeBoxCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.PlanFromScopeBox_V004_16.png"),
                ToolTip = "Plan From Scope Box (V004)"
            };
            var scopeBoxV003 = new PushButtonData("Btn_PlanFromScopeBox.V003", "Scope Box (V003)", assemblyPath, "Revit26_Plugin.PlanFromScopeBox.V003.Commands.PlanFromScopeBoxCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.PlanFromScopeBox_16.png"),
                ToolTip = "Plan From Scope Box (V003)"
            };
            var scopeBoxSplitData = RibbonLayoutHelper.CreateSplitButtonData("Split_PlanFromScopeBox", "Scope Box", scopeBoxV004);

            var sheetPlacerV222 = new PushButtonData("Btn_SmartViewToSheetPlacer.V222", "View To Sheet (V222)", assemblyPath, "Revit26_Plugin.SmartViewToSheetPlacer.V222.SmartViewToSheetPlacerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SmartViewToSheetPlacer_V222_16.png"),
                ToolTip = "Smart View To Sheet Placer (V222)"
            };
            var sheetPlacerV221 = new PushButtonData("Btn_SmartViewToSheetPlacer.V221", "Sheet Placer (V221)", assemblyPath, "Revit26_Plugin.SmartViewToSheetPlacer.V221.SmartViewToSheetPlacerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SmartViewToSheetPlacer_16.png"),
                ToolTip = "Smart View To Sheet Placer (V221)"
            };
            var sheetPlacerSplitData = RibbonLayoutHelper.CreateSplitButtonData("Split_SmartViewToSheetPlacer", "Sheet Placer", sheetPlacerV222);

            // Kept as two standalone buttons rather than pairing them into one
            // 2-item stack — only proven-safe placement for a split button is
            // paired with a plain push button, or entirely on its own.
            var scopeBoxItems = RibbonLayoutHelper.AddStackedButtons(createPanel, new List<RibbonItemData> { scopeBoxSplitData });
            RibbonLayoutHelper.WireSplitButton(scopeBoxItems, "Split_PlanFromScopeBox", scopeBoxV004, scopeBoxV003);

            var sheetPlacerItems = RibbonLayoutHelper.AddStackedButtons(createPanel, new List<RibbonItemData> { sheetPlacerSplitData });
            RibbonLayoutHelper.WireSplitButton(sheetPlacerItems, "Split_SmartViewToSheetPlacer", sheetPlacerV222, sheetPlacerV221);

            RibbonPanel placePanel = app.CreateRibbonPanel(tabName, "Sheet Place");

            var rearrangeV025 = new PushButtonData("Btn_SheetAutoRearrange.V025", "Rearrange (V025)", assemblyPath, "Revit26_Plugin.SheetAutoRearrange.V025.Commands.SheetAutoRearrangeCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SheetAutoRearrange_V025_16.png"),
                ToolTip = "Sheet Auto Rearrange (V025)"
            };
            var rearrangeV024 = new PushButtonData("Btn_SheetAutoRearrange.V024", "Rearrange (V024)", assemblyPath, "Revit26_Plugin.SheetAutoRearrange.V024.Commands.SheetAutoRearrangeCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SheetAutoRearrange_16.png"),
                ToolTip = "Sheet Auto Rearrange (V024)"
            };
            var rearrangeSplitData = RibbonLayoutHelper.CreateSplitButtonData("Split_SheetAutoRearrange", "Rearrange", rearrangeV025);

            var placeItems = RibbonLayoutHelper.AddStackedButtons(placePanel, new List<RibbonItemData> { rearrangeSplitData });
            RibbonLayoutHelper.WireSplitButton(placeItems, "Split_SheetAutoRearrange", rearrangeV025, rearrangeV024);
        }
    }
}
