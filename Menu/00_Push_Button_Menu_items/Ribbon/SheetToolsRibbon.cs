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
            SheetCreate.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SBtoSheet.png");

            //Sheet Reaarngeger
            SheetCreate.AddPushButton(new PushButtonData("Btn_PlanFromScopeBox.V002", "Plan From ScopeBox.V002 #", assemblyPath, "Revit26_Plugin.PlanFromScopeBox.V002.Commands.PlanFromScopeBoxCommand"));            
            SheetCreate.AddPushButton(new PushButtonData("Btn_SmartViewToSheetPlacer.V213", "Smart View To Sheet Placer V213 #", assemblyPath, "Revit26_Plugin.SmartViewToSheetPlacer.V213.SmartViewToSheetPlacerCommand"));

            //Place Sections Menu
            PulldownButton SheetPlace = panel.AddItem(new PulldownButtonData("Place", "Place")) as PulldownButton;
            SheetPlace.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewToSheet.png");

            SheetPlace.AddPushButton(new PushButtonData("Btn_SheetAutoRearrange.V002", "Sheet Auto Rearrange_V002", assemblyPath, "Revit26_Plugin.SheetAutoRearrange.V002.Commands.SheetAutoRearrangeCommand"));
            SheetPlace.AddPushButton(new PushButtonData("Btn_SheetAutoRearrange.V003", "Sheet Auto Rearrange_V003", assemblyPath, "Revit26_Plugin.SheetAutoRearrange.V003.Commands.SheetAutoRearrangeCommand"));
            SheetPlace.AddPushButton(new PushButtonData("Btn_SheetAutoRearrange.V006", "Sheet Auto Rearrange_V006", assemblyPath, "Revit26_Plugin.SheetAutoRearrange.V006.Commands.SheetAutoRearrangeCommand"));





        }
    }
}
