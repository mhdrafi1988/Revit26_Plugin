using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
using Revit26_Plugin.Shared.Services;
using System.Collections.Generic;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class SheetToolsRibbon
    {
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel createPanel = app.CreateRibbonPanel(tabName, "Sheet Create");

            var scopeBoxV004 = new PushButtonData("Btn_PlanFromScopeBox.V004", "Scope Box", assemblyPath, "Revit26_Plugin.PlanFromScopeBox.V004.Commands.PlanFromScopeBoxCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.PlanFromScopeBox_V004_16.png"),
                ToolTip = ToolCatalog.PlanFromScopeBox.Tip()
            };
            var sheetPlacerV222 = new PushButtonData("Btn_SmartViewToSheetPlacer.V222", "Sheet Placer", assemblyPath, "Revit26_Plugin.SmartViewToSheetPlacer.V222.SmartViewToSheetPlacerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SmartViewToSheetPlacer_V222_16.png"),
                ToolTip = ToolCatalog.SmartViewToSheetPlacer.Tip()
            };
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
                ToolTip = ToolCatalog.AutomatedSectionPlacer.Tip("Detects section views visible on the active Plan View and places them onto new or existing sheets")
            };

            RibbonLayoutHelper.AddStackedButtons(createPanel, new List<RibbonItemData> { scopeBoxV004, sheetPlacerV222, autoSectionPlacerV001 });

            RibbonPanel placePanel = app.CreateRibbonPanel(tabName, "Sheet Place");

            var rearrangeV026 = new PushButtonData("Btn_SheetAutoRearrange.V026", "Rearrange", assemblyPath, "Revit26_Plugin.SheetAutoRearrange.V026.Commands.SheetAutoRearrangeCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SheetAutoRearrange_V026_16.png"),
                ToolTip = ToolCatalog.SheetAutoRearrange.Tip("Adds Priority Groups (rank ViewTypes so e.g. all Sections place before any Drafting Views)")
            };
            RibbonLayoutHelper.AddStackedButtons(placePanel, new List<RibbonItemData> { rearrangeV026 });
        }
    }
}
