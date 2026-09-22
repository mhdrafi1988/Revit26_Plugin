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
            // each version is a push button inside the dropdown.
            var scopeBoxV004 = new PushButtonData("Btn_PlanFromScopeBox.V004", "Scope Box V004", assemblyPath, "Revit26_Plugin.PlanFromScopeBox.V004.Commands.PlanFromScopeBoxCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.PlanFromScopeBox_V004_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Plan From Scope Box", "V004")
            };
            var scopeBoxV003 = new PushButtonData("Btn_PlanFromScopeBox.V003", "Scope Box V003", assemblyPath, "Revit26_Plugin.PlanFromScopeBox.V003.Commands.PlanFromScopeBoxCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.PlanFromScopeBox_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Plan From Scope Box", "V003")
            };
            var scopeBoxPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_PlanFromScopeBox", "Scope Box", scopeBoxV004);

            var sheetPlacerV222 = new PushButtonData("Btn_SmartViewToSheetPlacer.V222", "Sheet Placer V222", assemblyPath, "Revit26_Plugin.SmartViewToSheetPlacer.V222.SmartViewToSheetPlacerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SmartViewToSheetPlacer_V222_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Smart View To Sheet Placer", "V222")
            };
            var sheetPlacerV221 = new PushButtonData("Btn_SmartViewToSheetPlacer.V221", "Sheet Placer V221", assemblyPath, "Revit26_Plugin.SmartViewToSheetPlacer.V221.SmartViewToSheetPlacerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SmartViewToSheetPlacer_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Smart View To Sheet Placer", "V221")
            };
            var sheetPlacerPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_SmartViewToSheetPlacer", "Sheet Placer", sheetPlacerV222);

            // Automated Section Placer V001: new standalone tool (not a new
            // SmartViewToSheetPlacer version) — detects section views visible
            // on the active Plan View and runs them through the same
            // packing/placement pipeline. Single version so far, no pulldown
            // needed — same single-button pattern as CombinedRoofTools_V001.
            var autoSectionPlacerV001 = new PushButtonData("Btn_AutomatedSectionPlacer_V001", "Auto Section Placer", assemblyPath, "Revit26_Plugin.AutomatedSectionPlacer.V001.AutomatedSectionPlacerCommand")
            {
                // TODO: swap for a dedicated icon — temporarily reusing the
                // Sheet Placer icon so the button isn't blank.
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SmartViewToSheetPlacer_V222_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Automated Section Placer", "V001",
                    "Detects section views visible on the active Plan View and places them onto new or existing sheets")
            };

            // Pulldown buttons can share a stack, so both tools now sit
            // together in one 2-item stack instead of two lone buttons.
            var sheetCreateItems = RibbonLayoutHelper.AddStackedButtons(createPanel, new List<RibbonItemData> { scopeBoxPulldownData, sheetPlacerPulldownData, autoSectionPlacerV001 });
            RibbonLayoutHelper.WirePulldownButton(sheetCreateItems, "Pulldown_PlanFromScopeBox", scopeBoxV004, scopeBoxV003);
            RibbonLayoutHelper.WirePulldownButton(sheetCreateItems, "Pulldown_SmartViewToSheetPlacer", sheetPlacerV222, sheetPlacerV221);

            RibbonPanel placePanel = app.CreateRibbonPanel(tabName, "Sheet Place");

            var rearrangeV026 = new PushButtonData("Btn_SheetAutoRearrange.V026", "Rearrange V026", assemblyPath, "Revit26_Plugin.SheetAutoRearrange.V026.Commands.SheetAutoRearrangeCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SheetAutoRearrange_V026_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Sheet Auto Rearrange", "V026",
                    "Adds Priority Groups (rank ViewTypes so e.g. all Sections place before any Drafting Views)")
            };
            var rearrangeV025 = new PushButtonData("Btn_SheetAutoRearrange.V025", "Rearrange V025", assemblyPath, "Revit26_Plugin.SheetAutoRearrange.V025.Commands.SheetAutoRearrangeCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SheetAutoRearrange_V025_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Sheet Auto Rearrange", "V025")
            };
            var rearrangeV024 = new PushButtonData("Btn_SheetAutoRearrange.V024", "Rearrange V024", assemblyPath, "Revit26_Plugin.SheetAutoRearrange.V024.Commands.SheetAutoRearrangeCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SheetAutoRearrange_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Sheet Auto Rearrange", "V024")
            };
            var rearrangePulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_SheetAutoRearrange", "Rearrange", rearrangeV026);

            var placeItems = RibbonLayoutHelper.AddStackedButtons(placePanel, new List<RibbonItemData> { rearrangePulldownData });
            RibbonLayoutHelper.WirePulldownButton(placeItems, "Pulldown_SheetAutoRearrange", rearrangeV026, rearrangeV025, rearrangeV024);
        }
    }
}
