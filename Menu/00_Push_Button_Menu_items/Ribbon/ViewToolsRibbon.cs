using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class ViewToolsRibbon
    {
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = app.CreateRibbonPanel(tabName, "View Tools");

            PulldownButton ViewRename = panel.AddItem(new PulldownButtonData("Rename", "Rename")) as PulldownButton;
            ViewRename.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Rename_32.png");
            //ViewRename.Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.rename_32.png");

            ViewRename.AddPushButton(new PushButtonData("Btn_BubbleRenumberCommandV3", "Bubble Renumber Command V3", assemblyPath, "Revit22_Plugin.SDRV3.BubbleRenumberCommandV3"));
            ViewRename.AddPushButton(new PushButtonData("Btn_BubbleRenumberCommandV4", "Bubble Renumber Command V4", assemblyPath, "Revit26_Plugin.SDRV4.commands.BubbleRenumberCommandV4"));
            ViewRename.AddPushButton(new PushButtonData("Btn_SectionManagerRefactored", "Section Renamer 4.0 #4", assemblyPath, "Revit22_Plugin.SectionManagerMVVM_Refactored.SectionManagerCommandRefactored"));
            ViewRename.AddPushButton(new PushButtonData("Btn_SectionManagerRefactoredV2", "Section Renamer V2 4.0 #4", assemblyPath, "Revit26_Plugin.SectionRenamer_V02.SectionManagerEventManager"));
            ViewRename.AddPushButton(new PushButtonData("Btn_SectionManagerRefactoredV6", "Section Renamer V6 1.0 #6", assemblyPath, "Revit26_Plugin.SARV6.Commands.OpenSectionManagerCommand"));
            ViewRename.AddPushButton(new PushButtonData("Btn_SectionManagerRefactoredV7", "Section Renamer V7 **", assemblyPath, "Revit26_Plugin.SectionManager_V07.Commands.OpenSectionManagerCommand"));

            PulldownButton ViewCreate = panel.AddItem(new PulldownButtonData("Create", "Create")) as PulldownButton;
            ViewCreate.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Create_32.png");

            ViewCreate.AddPushButton(new PushButtonData("Btn_CSFL_V07", "Create Sections From Detail Lines V07", assemblyPath, "Revit26_Plugin.CSFL_V07.Commands.CreateSectionsFromDetailLines"));
            ViewCreate.AddPushButton(new PushButtonData("Btn_CSFL_V08", "Create Sections From Detail Lines V08", assemblyPath, "Revit26_Plugin.CSFL_08.Commands.CreateSectionsFromDetailLinesCommand"));



            PulldownButton ViewPlace = panel.AddItem(new PulldownButtonData("Place", "Place")) as PulldownButton;
            ViewPlace.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Place_32.png");
            
            ViewPlace.AddPushButton(new PushButtonData("Btn_AutoPlaceSectionsCommand_311", "AutoPlaceSectionsCommand_311 #", assemblyPath, "Revit26_Plugin.APUS_V311.Commands.AutoPlaceSectionsCommand"));
            ViewPlace.AddPushButton(new PushButtonData("Btn_AutoPlaceSectionsCommand_312", "AutoPlaceSectionsCommand_312 #", assemblyPath, "Revit26_Plugin.APUS_V312.Commands.AutoPlaceSectionsCommand"));
            ViewPlace.AddPushButton(new PushButtonData("Btn_AutoPlaceSectionsCommand_313", "AutoPlaceSectionsCommand_313 #", assemblyPath, "Revit26_Plugin.APUS_V313.Commands.AutoPlaceSectionsCommand"));

            ViewPlace.AddPushButton(new PushButtonData("Btn_COPV6", "COPV6 #", assemblyPath, "Revit26_Plugin.CalloutCOP_V06.Commands.CalloutCOPCommand"));
        }
    }
}
