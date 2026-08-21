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
            ViewCreate.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.Create_32.png");
           
            ViewCreate.AddPushButton(new PushButtonData("Btn_ Sections From Detail Lines V11", "Create Sections From Detail Lines(With Floor And Roof) V11 #", assemblyPath, "Revit26_Plugin.CreateSections.V011.Commands.CreateSectionsFromDetailLines"));
            //Place Sections Menu
            PulldownButton ViewPlace = panel.AddItem(new PulldownButtonData("Place", "Place")) as PulldownButton;
            ViewPlace.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.Place_32.png");
            
            ViewPlace.AddPushButton(new PushButtonData("Btn_AutoPlaceSectionsCommand_V321_01", "AutoPlaceSectionsCommand_V321_01 ##", assemblyPath, "Revit26_Plugin.APUS_V321_01.Commands.AutoPlaceSectionsCommand"));
            ViewPlace.AddPushButton(new PushButtonData("Btn_AutoPlaceSectionsCommand_V322_01", "AutoPlaceSectionsCommand_V322_01 ##", assemblyPath, "Revit26_Plugin.APUS.V322.Commands.AutoPlaceSectionsCommand"));

            ViewPlace.AddPushButton(new PushButtonData("Btn_CalloutToSectionViewPlacement_V019 ###", "CalloutToSectionViewPlacement V019 #", assemblyPath, "Revit26_Plugin.CalloutCOP.V019.Commands.CalloutCOPCommand"));

            ViewPlace.AddPushButton(new PushButtonData("Btn_RefSectionHeadPlacerCommand V013", "RefSectionHeadPlacerCommand V013 *** #", assemblyPath, "Revit26_Plugin.RefSectionHeadPlacer.V013.Commands.RefSectionHeadPlacerCommand"));

            ViewPlace.AddPushButton(new PushButtonData("Btn_SectionViewAutoTagger.V003", "SectionViewAutoTagger.V003 #", assemblyPath, "Revit26_Plugin.SectionViewAutoTagger.V003.SectionViewAutoTaggerCommand"));
            ViewPlace.AddPushButton(new PushButtonData("Btn_SectionViewAutoTagger.V004", "SectionViewAutoTagger.V004 #", assemblyPath, "Revit26_Plugin.SectionViewAutoTagger.V004.SectionViewAutoTaggerCommand"));
            //Rename Sections Menu
            PulldownButton ViewRename = panel.AddItem(new PulldownButtonData("Rename", "Rename")) as PulldownButton;
            ViewRename.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.Rename_32.png");

            //ViewRename.Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.rename_32.png");
            ViewRename.AddPushButton(new PushButtonData("Btn_BubbleAutoRenumber_V006", "Bubble Renumber Command V501 V006", assemblyPath, "Revit26_Plugin.BubbleAutoRenumber.V006.Commands.SectionAutoRenumberCommand"));
            ViewRename.AddPushButton(new PushButtonData("Btn_SectionAutoRenamer_V024", "Section Auto Renamer V024", assemblyPath, "Revit26_Plugin.SectionAutoRenamer.V024.Commands.OpenSectionManagerCommand"));
            ViewRename.AddPushButton(new PushButtonData("Btn_SectionManager_V008", "Section Manager V008", assemblyPath, "Revit26_Plugin.SectionManager.V008.Commands.OpenSectionManagerCommand"));
            ViewRename.AddPushButton(new PushButtonData("Btn_SectionManagerRefactor_V002", "Section Manager Refactor V002", assemblyPath, "Revit26_Plugin.SectionManagerRefactor.V002.SectionManagerCommandRefactored"));
            ViewRename.AddPushButton(new PushButtonData("Btn_ViewAutoRenamer_V003", "View Auto Renamer V003", assemblyPath, "Revit26_Plugin.ViewAutoRenamer.V003.Commands.OpenViewAutoRenamerCommand"));

        }
    }
}
