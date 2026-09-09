using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class SheetToolsRibbon
    {
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = app.CreateRibbonPanel(tabName, "Sheet Tools");
            //Sheet placer
            PulldownButton SheetCreate = panel.AddItem(new PulldownButtonData("Create", "Create")) as PulldownButton;
            SheetCreate.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.SBtoSheet.png");

            //Sheet Rearrange
            SheetCreate.AddPushButton(new PushButtonData("Btn_PlanFromScopeBox.V003", "Plan From Scope Box — V003", assemblyPath, "Revit26_Plugin.PlanFromScopeBox.V003.Commands.PlanFromScopeBoxCommand"));
            SheetCreate.AddPushButton(new PushButtonData("Btn_PlanFromScopeBox.V004", "Plan From Scope Box — V004", assemblyPath, "Revit26_Plugin.PlanFromScopeBox.V004.Commands.PlanFromScopeBoxCommand"));
            SheetCreate.AddPushButton(new PushButtonData("Btn_SmartViewToSheetPlacer.V221", "Smart View To Sheet Placer — V221", assemblyPath, "Revit26_Plugin.SmartViewToSheetPlacer.V221.SmartViewToSheetPlacerCommand"));
            SheetCreate.AddPushButton(new PushButtonData("Btn_SmartViewToSheetPlacer.V222", "Smart View To Sheet Placer — V222", assemblyPath, "Revit26_Plugin.SmartViewToSheetPlacer.V222.SmartViewToSheetPlacerCommand"));

            //Place Sections Menu
            PulldownButton SheetPlace = panel.AddItem(new PulldownButtonData("Place", "Place")) as PulldownButton;
            SheetPlace.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetTools.ViewToSheet.png");

            SheetPlace.AddPushButton(new PushButtonData("Btn_SheetAutoRearrange.V024", "Sheet Auto Rearrange — V024", assemblyPath, "Revit26_Plugin.SheetAutoRearrange.V024.Commands.SheetAutoRearrangeCommand"));
            SheetPlace.AddPushButton(new PushButtonData("Btn_SheetAutoRearrange.V025", "Sheet Auto Rearrange — V025", assemblyPath, "Revit26_Plugin.SheetAutoRearrange.V025.Commands.SheetAutoRearrangeCommand"));
        }
    }
}
