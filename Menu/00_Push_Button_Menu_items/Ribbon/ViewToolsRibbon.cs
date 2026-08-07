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

            ViewCreate.AddPushButton(new PushButtonData("Btn_Sections From Detail_V07", "Sections From Detail Lines V07*", assemblyPath, "Revit26_Plugin.CreateSectionsFromDetailLines.V07.Commands.CreateSectionsFromDetailLines"));
            ViewCreate.AddPushButton(new PushButtonData("Btn_ Sections From Detail V08", "Create Sections From Detail Lines V08*", assemblyPath, "Revit26_Plugin.CreateSectionsFromDetailLines.V008.Commands.CreateSectionsFromDetailLines"));
            ViewCreate.AddPushButton(new PushButtonData("Btn_ Sections From Detail Lines V09", "Create Sections From Detail Lines V09*", assemblyPath, "Revit26_Plugin.CreateSectionsFromDetailLines.V009.Commands.CreateSectionsFromDetailLines"));
            ViewCreate.AddPushButton(new PushButtonData("Btn_RoofEdgeAroundSections_V001 ###", "RoofEdgeAroundSections_V001 #", assemblyPath, "Revit26_Plugin.RoofEdgeSections.V001.RoofEdgeAroundSectionsCommand"));
            ViewCreate.AddPushButton(new PushButtonData("Btn_RoofEdgeAroundSections_V002 ###", "RoofEdgeAroundSections_V002 #", assemblyPath, "Revit26_Plugin.RoofEdgeSections.V002.RoofEdgeAroundSectionsCommand"));

            //Place Sections Menu
            PulldownButton ViewPlace = panel.AddItem(new PulldownButtonData("Place", "Place")) as PulldownButton;
            ViewPlace.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Place_32.png");  
            
            ViewPlace.AddPushButton(new PushButtonData("Btn_AutoPlaceSectionsCommand_V321_01", "AutoPlaceSectionsCommand_V321_01 ##", assemblyPath, "Revit26_Plugin.APUS_V321_01.Commands.AutoPlaceSectionsCommand"));
            ViewPlace.AddPushButton(new PushButtonData("Btn_CalloutToSectionViewPlacement_V017 ###", "CalloutToSectionViewPlacement V017 #", assemblyPath, "Revit26_Plugin.CalloutCOP.V017.Commands.CalloutCOPCommand"));
            ViewPlace.AddPushButton(new PushButtonData("Btn_RoofDrainCalloutPlacing.V001", "RoofDrainCalloutPlacing V001 #", assemblyPath, "Revit26_Plugin.RoofDrainCalloutPlacing.V001.Commands.RoofDrainCalloutPlacingCommand"));
            ViewPlace.AddPushButton(new PushButtonData("Btn_RefSectionHeadPlacerCommand V012", "RefSectionHeadPlacerCommand V012 *** #", assemblyPath, "Revit26_Plugin.RefSectionHeadPlacer.V012.Commands.RefSectionHeadPlacerCommand"));
             //Rename Sections Menu
            PulldownButton ViewRename = panel.AddItem(new PulldownButtonData("Rename", "Rename")) as PulldownButton;
            ViewRename.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Rename_32.png");

            //ViewRename.Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.rename_32.png");
            ViewRename.AddPushButton(new PushButtonData("Btn_BubbleRenumberCommand501", "Bubble Renumber Command V501", assemblyPath, "Revit26_Plugin.SectionAutoRenumber.Commands.SectionAutoRenumberCommand"));
            ViewRename.AddPushButton(new PushButtonData("Btn_SectionAutoRenamer_12", "Section Auto Renamer v12 **", assemblyPath, "Revit26_Plugin.SectionAutoRenamer.V012.Commands.OpenSectionManagerCommand"));

        }
    }
}
