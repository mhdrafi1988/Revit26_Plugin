using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class SheetToolsRibbon
    {
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = app.CreateRibbonPanel(tabName, "Sheet Tools");
            //Create Sections Menu
            PulldownButton SheetCreate = panel.AddItem(new PulldownButtonData("Create", "Create")) as PulldownButton;
            SheetCreate.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SBtoSheet.png");

            SheetCreate.AddPushButton(new PushButtonData("Btn_PlanFromScopeBox.V001", "Plan From ScopeBox.V001", assemblyPath, "Revit26_Plugin.PlanFromScopeBox.V001.Commands.PlanFromScopeBoxCommand"));
            SheetCreate.AddPushButton(new PushButtonData("Btn_PlanFromScopeBox.V002", "Plan From ScopeBox.V002 #", assemblyPath, "Revit26_Plugin.PlanFromScopeBox.V002.Commands.PlanFromScopeBoxCommand"));
            SheetCreate.AddPushButton(new PushButtonData("Btn_SmartViewToSheetPlacer.V201", "Smart View To Sheet Placer V201 #", assemblyPath, "Revit26_Plugin.SmartViewToSheetPlacer.V201.SmartViewToSheetPlacerCommand"));
            SheetCreate.AddPushButton(new PushButtonData("Btn_SmartViewToSheetPlacer.V202", "Smart View To Sheet Placer V202 #", assemblyPath, "Revit26_Plugin.SmartViewToSheetPlacer.V202.SmartViewToSheetPlacerCommand"));
            SheetCreate.AddPushButton(new PushButtonData("Btn_SmartViewToSheetPlacer.V204", "Smart View To Sheet Placer V204 #", assemblyPath, "Revit26_Plugin.SmartViewToSheetPlacer.V204.SmartViewToSheetPlacerCommand"));
            



            SheetCreate.AddPushButton(new PushButtonData("Btn_ViewSheetPlacer", "ViewSheetPlacer_V001 ", assemblyPath, "Revit26_Plugin.Tools.ViewSheetPlacer.ViewSheetPlacerCommand"));

            //Place Sections Menu
            PulldownButton SheetPlace = panel.AddItem(new PulldownButtonData("Place", "Place")) as PulldownButton;
            SheetPlace.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewToSheet.png");
            SheetPlace .AddPushButton(new PushButtonData("Btn_BubbleRenumberCommand503", "Bubble Renumber Command V503", assemblyPath, "Revit26_Plugin.SectionAutoRenumber.Commands.SectionAutoRenumberCommand"));

            //Rename Sections Menu
            PulldownButton SheetRename = panel.AddItem(new PulldownButtonData("Rename", "Rename")) as PulldownButton;
            SheetRename.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SheetSettings.png");
            //SheetRename.Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.rename_32.png");
            SheetRename.AddPushButton(new PushButtonData("Btn_BubbleRenumberCommand501", "Bubble Renumber Command V501", assemblyPath, "Revit26_Plugin.SectionAutoRenumber.Commands.SectionAutoRenumberCommand"));
            


        }
    }
}
