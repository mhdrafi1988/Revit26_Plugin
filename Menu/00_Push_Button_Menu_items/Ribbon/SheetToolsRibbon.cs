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

            // Coexisting versions of each tool share one pulldown button; the
            // previous version stays first and the UI Standard version is second.
            var scopeBoxV004 = new PushButtonData("Btn_PlanFromScopeBox.V004", "Scope Box V004", assemblyPath, "Revit26_Plugin.PlanFromScopeBox.V004.Commands.PlanFromScopeBoxCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.PlanFromScopeBox_V004_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Plan From Scope Box", "V004")
            };
            var scopeBoxV005 = new PushButtonData("Btn_PlanFromScopeBox.V005", "Scope Box V005", assemblyPath, "Revit26_Plugin.PlanFromScopeBox.V005.Commands.PlanFromScopeBoxCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.PlanFromScopeBox_V004_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Plan From Scope Box", "V005", "UI Standard layout: fixed header and metrics, footer log with Copy All / Copy Selected, Create → Close.")
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
            var sheetPlacerV223 = new PushButtonData("Btn_SmartViewToSheetPlacer.V223", "Sheet Placer V223", assemblyPath, "Revit26_Plugin.SmartViewToSheetPlacer.V223.SmartViewToSheetPlacerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SmartViewToSheetPlacer_V222_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Smart View To Sheet Placer", "V223", "UI Standard layout: fixed footer with the Activity Log (Copy All / Copy Selected), Export Logs and Close.")
            };
            var sheetPlacerV221 = new PushButtonData("Btn_SmartViewToSheetPlacer.V221", "Sheet Placer V221", assemblyPath, "Revit26_Plugin.SmartViewToSheetPlacer.V221.SmartViewToSheetPlacerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SmartViewToSheetPlacer_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Smart View To Sheet Placer", "V221")
            };
            var sheetPlacerPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_SmartViewToSheetPlacer", "Sheet Placer", sheetPlacerV222);

            // Automated Section Placer: standalone tool (not a SmartViewToSheetPlacer
            // version) — detects section views visible on the active Plan View and
            // runs them through the same packing/placement pipeline.
            // TODO: swap for a dedicated icon — temporarily reusing the Sheet Placer icon.
            var autoSectionPlacerV001 = new PushButtonData("Btn_AutomatedSectionPlacer_V001", "Auto Section Placer V001", assemblyPath, "Revit26_Plugin.AutomatedSectionPlacer.V001.AutomatedSectionPlacerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SmartViewToSheetPlacer_V222_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Automated Section Placer", "V001",
                    "Detects section views visible on the active Plan View and places them onto new or existing sheets")
            };
            var autoSectionPlacerV002 = new PushButtonData("Btn_AutomatedSectionPlacer_V002", "Auto Section Placer V002", assemblyPath, "Revit26_Plugin.AutomatedSectionPlacer.V002.AutomatedSectionPlacerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SmartViewToSheetPlacer_V222_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Automated Section Placer", "V002", "UI Standard layout: fixed footer with the Activity Log (Copy All / Copy Selected), Export Logs and Close.")
            };
            var autoSectionPlacerPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_AutomatedSectionPlacer", "Auto Section Placer", autoSectionPlacerV001);

            var sheetCreateItems = RibbonLayoutHelper.AddStackedButtons(createPanel, new List<RibbonItemData> { scopeBoxPulldownData, sheetPlacerPulldownData, autoSectionPlacerPulldownData });
            RibbonLayoutHelper.WirePulldownButton(sheetCreateItems, "Pulldown_PlanFromScopeBox", scopeBoxV004, scopeBoxV005, scopeBoxV003);
            RibbonLayoutHelper.WirePulldownButton(sheetCreateItems, "Pulldown_SmartViewToSheetPlacer", sheetPlacerV222, sheetPlacerV223, sheetPlacerV221);
            RibbonLayoutHelper.WirePulldownButton(sheetCreateItems, "Pulldown_AutomatedSectionPlacer", autoSectionPlacerV001, autoSectionPlacerV002);

            RibbonPanel placePanel = app.CreateRibbonPanel(tabName, "Sheet Place");

            var rearrangeV026 = new PushButtonData("Btn_SheetAutoRearrange.V026", "Rearrange V026", assemblyPath, "Revit26_Plugin.SheetAutoRearrange.V026.Commands.SheetAutoRearrangeCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SheetAutoRearrange_V026_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Sheet Auto Rearrange", "V026",
                    "Adds Priority Groups (rank ViewTypes so e.g. all Sections place before any Drafting Views)")
            };
            var rearrangeV027 = new PushButtonData("Btn_SheetAutoRearrange.V027", "Rearrange V027", assemblyPath, "Revit26_Plugin.SheetAutoRearrange.V027.Commands.SheetAutoRearrangeCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SheetAutoRearrange_V026_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Sheet Auto Rearrange", "V027", "UI Standard layout: metrics on top, always-visible log in the footer (Copy All / Copy Selected), Run → Close.")
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
            RibbonLayoutHelper.WirePulldownButton(placeItems, "Pulldown_SheetAutoRearrange", rearrangeV026, rearrangeV027, rearrangeV025, rearrangeV024);
        }
    }
}
