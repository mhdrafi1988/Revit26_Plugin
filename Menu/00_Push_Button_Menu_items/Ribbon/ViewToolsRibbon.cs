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
           
            ViewCreate.AddPushButton(new PushButtonData("Btn_ Sections From Detail Lines V11", "Create Sections From Detail Lines — V011", assemblyPath, "Revit26_Plugin.CreateSections.V011.Commands.CreateSectionsFromDetailLines")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.SectionsFromDetailLines_32.png")
            });
            ViewCreate.AddPushButton(new PushButtonData("Btn_RoofEdgeAroundSections_V003", "Roof Edge Around Sections — V003", assemblyPath, "Revit26_Plugin.RoofEdgeAroundSections.V003.RoofEdgeAroundSectionsCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.RoofEdgeAroundSections_32.png")
            });
            ViewCreate.AddPushButton(new PushButtonData("Btn_RoofEdgeAroundSections_V004", "Roof Edge Around Sections — V004", assemblyPath, "Revit26_Plugin.RoofEdgeAroundSections.V004.RoofEdgeAroundSectionsCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.RoofEdgeAroundSections_V004_32.png")
            });
            ViewCreate.AddPushButton(new PushButtonData("Btn_RoofEdgeElementSections_V001", "Roof Edge Element Sections — V001", assemblyPath, "Revit26_Plugin.RoofEdgeElementSections.V001.RoofEdgeElementSectionsCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.RoofEdgeElementSections_32.png")
            });
            //Place Sections Menu
            PulldownButton ViewPlace = panel.AddItem(new PulldownButtonData("Place", "Place")) as PulldownButton;
            ViewPlace.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.Place_32.png");

            ViewPlace.AddPushButton(new PushButtonData("Btn_AutoPlaceSectionsCommand_V322_01", "Auto Place Sections — V322", assemblyPath, "Revit26_Plugin.APUS.V322.Commands.AutoPlaceSectionsCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.AutoPlaceSections_32.png")
            });
            ViewPlace.AddPushButton(new PushButtonData("Btn_CalloutToSectionViewPlacement_V019", "Callout To Section View Placement — V019", assemblyPath, "Revit26_Plugin.CalloutCOP.V019.Commands.CalloutCOPCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.CalloutToSection_32.png")
            });
            ViewPlace.AddPushButton(new PushButtonData("Btn_RefSectionHeadPlacerCommand V013", "Reference Section Head Placer — V013", assemblyPath, "Revit26_Plugin.RefSectionHeadPlacer.V013.Commands.RefSectionHeadPlacerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.RefSectionHeadPlacer_32.png")
            });
            ViewPlace.AddPushButton(new PushButtonData("Btn_SectionViewAutoTagger.V004", "Section View Auto Tagger — V004", assemblyPath, "Revit26_Plugin.SectionViewAutoTagger.V004.SectionViewAutoTaggerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.SectionAutoTagger_32.png")
            });

            //Rename Sections Menu
            PulldownButton ViewRename = panel.AddItem(new PulldownButtonData("Rename", "Rename")) as PulldownButton;
            ViewRename.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.Rename_32.png");

            ViewRename.AddPushButton(new PushButtonData("Btn_BubbleAutoRenumber_V006", "Bubble Auto Renumber — V006", assemblyPath, "Revit26_Plugin.BubbleAutoRenumber.V006.Commands.SectionAutoRenumberCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.BubbleAutoRenumber_32.png")
            });
            ViewRename.AddPushButton(new PushButtonData("Btn_SectionAutoRenamer_V024", "Section Auto Renamer — V024", assemblyPath, "Revit26_Plugin.SectionAutoRenamer.V024.Commands.OpenSectionManagerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.SectionAutoRenamer_32.png")
            });


            ViewRename.AddPushButton(new PushButtonData("Btn_ViewAutoRenamer_V003", "View Auto Renamer — V003", assemblyPath, "Revit26_Plugin.ViewAutoRenamer.V003.Commands.OpenViewAutoRenamerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.ViewAutoRenamer_32.png")
            });
            ViewRename.AddPushButton(new PushButtonData("Btn_ViewAutoRenamer_V004", "View Auto Renamer — V004", assemblyPath, "Revit26_Plugin.ViewAutoRenamer.V004.Commands.OpenViewAutoRenamerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.ViewAutoRenamer_V004_32.png")
            });

        }
    }
}
