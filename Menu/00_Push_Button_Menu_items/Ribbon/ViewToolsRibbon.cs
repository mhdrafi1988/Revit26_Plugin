using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class ViewToolsRibbon
    {
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = app.CreateRibbonPanel(tabName, "View Tools");

            //Create Sections Menu
            PulldownButton ViewCreate = panel.AddItem(new PulldownButtonData("Create", "Create")) as PulldownButton;
            ViewCreate.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Create_32.png");

            ViewCreate.AddPushButton(new PushButtonData("Btn_CSFL_V07", "Create Sections From Detail Lines V07*", assemblyPath, "Revit26_Plugin.CreateSectionsFromDetailLines.V07.Commands.CreateSectionsFromDetailLines"));

            //Place Sections Menu
            PulldownButton ViewPlace = panel.AddItem(new PulldownButtonData("Place", "Place")) as PulldownButton;
            ViewPlace.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Place_32.png");

            //ViewPlace.AddPushButton(new PushButtonData("Btn_AutoPlaceSectionsCommand_314", "AutoPlaceSectionsCommand_314 ", assemblyPath, "Revit26_Plugin.APUS_V314.Commands.AutoPlaceSectionsCommand"));
            //ViewPlace.AddPushButton(new PushButtonData("Btn_AutoPlaceSectionsCommand_320", "AutoPlaceSectionsCommand_320_Grid Only #", assemblyPath, "Revit26_Plugin.APUS_V320.Commands.AutoPlaceSectionsCommand"));
            //ViewPlace.AddPushButton(new PushButtonData("Btn_AutoPlaceSectionsCommand_330", "AutoPlaceSectionsCommand_330 ONE  #", assemblyPath, "Revit26_Plugin.APUS_V330.Commands.AutoPlaceSectionsCommand"));
           // ViewPlace.AddPushButton(new PushButtonData("Btn_AutoPlaceSectionsCommand_V320_01", "AutoPlaceSectionsCommand_V320_01  #", assemblyPath, "Revit26_Plugin.APUS_V320_01.Commands.AutoPlaceSectionsCommand"));
            ViewPlace.AddPushButton(new PushButtonData("Btn_AutoPlaceSectionsCommand_V321_01", "AutoPlaceSectionsCommand_V321_01 ##", assemblyPath, "Revit26_Plugin.APUS_V321_01.Commands.AutoPlaceSectionsCommand"));
            ViewPlace.AddPushButton(new PushButtonData("Btn_CalloutCOP", "Call Out PLacing Auto V06 #", assemblyPath, "Revit26_Plugin.CalloutCOP_V06.Commands.CalloutCOPCommand"));
            


            ViewPlace.AddPushButton(new PushButtonData("Btn_CalloutCOP_V007", "Call Out PLacing Auto V007 ###", assemblyPath, "Revit26_Plugin.CalloutCOP_V007.Commands.CalloutAutoPlacer"));
            //ViewPlace.AddPushButton(new PushButtonData("Btn_CalloutCOP_V008", "Call Out PLacing Auto V008 #", assemblyPath, "Revit26_Plugin.CalloutPlacing_V008.Commands.CalloutAutoPlacer"));
            ViewPlace.AddPushButton(new PushButtonData("Btn_CalloutCOP_V011", "Call Out PLacing Auto V011 #", assemblyPath, "Revit26_Plugin.CalloutCOP.V011.Commands.CalloutCOPCommand"));
            //ViewPlace.AddPushButton(new PushButtonData("Btn_CalloutToSectionViewPlacement_V013 ###", "CalloutToSectionViewPlacement V013 #", assemblyPath, "Revit26_Plugin.CalloutToSectionViewPlacement.V013.Commands.CalloutCOPCommand"));
            //ViewPlace.AddPushButton(new PushButtonData("Btn_CalloutToSectionViewPlacement_V014 ###", "CalloutToSectionViewPlacement V014 #", assemblyPath, "Revit26_Plugin.CalloutCOP.V014.Commands.CalloutCOPCommand"));
            ViewPlace.AddPushButton(new PushButtonData("Btn_CalloutToSectionViewPlacement_V015 ###", "CalloutToSectionViewPlacement V015 #", assemblyPath, "Revit26_Plugin.CalloutCOP.V015.Commands.CalloutCOPCommand"));


            ViewPlace.AddPushButton(new PushButtonData("Btn_RoofDrainCalloutPlacing.V001", "RoofDrainCalloutPlacing V001 #", assemblyPath, "Revit26_Plugin.RoofDrainCalloutPlacing.V001.Commands.RoofDrainCalloutPlacingCommand"));

            ViewPlace.AddPushButton(new PushButtonData("Btn_ViewSheetPlacer", "ViewSheetPlacer*** #", assemblyPath, "Revit26_Plugin.Tools.ViewSheetPlacer.ViewSheetPlacerCommand"));
            ViewPlace.AddPushButton(new PushButtonData("Btn_PlanFromScopeBox", "PlanFromScopeBox *** #", assemblyPath, "Revit26_Plugin.PlanFromScopeBox.V001.Commands.PlanFromScopeBoxCommand"));
            ViewPlace.AddPushButton(new PushButtonData("Btn_RefSectionHeadPlacerCommand V001", "RefSectionHeadPlacerCommand V001 *** #", assemblyPath, "Revit26_Plugin.RefSectionHeadPlacer.V001.Commands.RefSectionHeadPlacerCommand"));
            ViewPlace.AddPushButton(new PushButtonData("Btn_RefSectionHeadPlacerCommand V002", "RefSectionHeadPlacerCommand V002 *** #", assemblyPath, "Revit26_Plugin.RefSectionHeadPlacer.V002.Commands.RefSectionHeadPlacerCommand"));


            //Rename Sections Menu
            PulldownButton ViewRename = panel.AddItem(new PulldownButtonData("Rename", "Rename")) as PulldownButton;
            ViewRename.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Rename_32.png");
            //ViewRename.Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.rename_32.png");

            //ViewRename.AddPushButton(new PushButtonData("Btn_BubbleRenumberCommandV3", "Bubble Renumber Command V3", assemblyPath, "Revit22_Plugin.SDRV3.BubbleRenumberCommandV3"));
            //ViewRename.AddPushButton(new PushButtonData("Btn_BubbleRenumberCommandV4", "Bubble Renumber Command V4", assemblyPath, "Revit26_Plugin.SDRV4.commands.BubbleRenumberCommandV4"));
            ViewRename.AddPushButton(new PushButtonData("Btn_BubbleRenumberCommand501", "Bubble Renumber Command V501", assemblyPath, "Revit26_Plugin.SectionAutoRenumber.Commands.SectionAutoRenumberCommand"));
            //ViewRename.AddPushButton(new PushButtonData("Btn_SectionManagerRefactored", "Section Renamer 4.0 #4", assemblyPath, "Revit22_Plugin.SectionManagerMVVM_Refactored.SectionManagerCommandRefactored"));
            //ViewRename.AddPushButton(new PushButtonData("Btn_SectionManagerRefactoredV2", "Section Renamer V2 4.0 #4", assemblyPath, "Revit26_Plugin.SectionRenamer_V02.SectionManagerEventManager"));
            //ViewRename.AddPushButton(new PushButtonData("Btn_SectionManagerRefactoredV6", "Section Renamer V6 1.0 #6", assemblyPath, "Revit26_Plugin.SARV6.Commands.OpenSectionManagerCommand"));
            //ViewRename.AddPushButton(new PushButtonData("Btn_SectionManagerRefactoredV7", "Section Renamer V7 **", assemblyPath, "Revit26_Plugin.SectionManager_V07.Commands.OpenSectionManagerCommand"));
            //ViewRename.AddPushButton(new PushButtonData("Btn_SectionAutoRenamer_09", "Section Auto Renamer v09 **", assemblyPath, "Revit26_Plugin.SectionAutoRenamer.V09.Commands.OpenSectionManagerCommand"));
            //ViewRename.AddPushButton(new PushButtonData("Btn_SectionAutoRenamer_10", "Section Auto Renamer v10 **", assemblyPath, "Revit26_Plugin.SectionAutoRenamer.V10.Commands.OpenSectionManagerCommand"));
            ViewRename.AddPushButton(new PushButtonData("Btn_SectionAutoRenamer_12", "Section Auto Renamer v12 **", assemblyPath, "Revit26_Plugin.SectionAutoRenamer.V012.Commands.OpenSectionManagerCommand"));






        }
    }
}
