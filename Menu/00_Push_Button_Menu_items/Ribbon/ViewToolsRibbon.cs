using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
using System.Collections.Generic;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class ViewToolsRibbon
    {
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel createPanel = app.CreateRibbonPanel(tabName, "View Create");
            RibbonLayoutHelper.AddStackedButtons(createPanel, new List<PushButtonData>
            {
                new PushButtonData("Btn_ Sections From Detail Lines V11", "Sections From Lines", assemblyPath, "Revit26_Plugin.CreateSections.V011.Commands.CreateSectionsFromDetailLines")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.SectionsFromDetailLines_16.png"),
                    ToolTip = "Create Sections From Detail Lines"
                },
                // Two coexisting implementations of the same tool — version
                // kept only here so the pair stays distinguishable.
                new PushButtonData("Btn_RoofEdgeAroundSections_V003", "Edge Around (V003)", assemblyPath, "Revit26_Plugin.RoofEdgeAroundSections.V003.RoofEdgeAroundSectionsCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.RoofEdgeAroundSections_16.png"),
                    ToolTip = "Roof Edge Around Sections (V003)"
                },
                new PushButtonData("Btn_RoofEdgeAroundSections_V004", "Edge Around (V004)", assemblyPath, "Revit26_Plugin.RoofEdgeAroundSections.V004.RoofEdgeAroundSectionsCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.RoofEdgeAroundSections_V004_16.png"),
                    ToolTip = "Roof Edge Around Sections (V004)"
                },
                new PushButtonData("Btn_RoofEdgeElementSections_V001", "Roof Edge Element Sections", assemblyPath, "Revit26_Plugin.RoofEdgeElementSections.V001.RoofEdgeElementSectionsCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.RoofEdgeElementSections_16.png")
                },
            });

            RibbonPanel placePanel = app.CreateRibbonPanel(tabName, "View Place");
            RibbonLayoutHelper.AddStackedButtons(placePanel, new List<PushButtonData>
            {
                new PushButtonData("Btn_AutoPlaceSectionsCommand_V322_01", "Auto Place Sections", assemblyPath, "Revit26_Plugin.APUS.V322.Commands.AutoPlaceSectionsCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.AutoPlaceSections_16.png")
                },
                new PushButtonData("Btn_CalloutToSectionViewPlacement_V019", "Callout To Section", assemblyPath, "Revit26_Plugin.CalloutCOP.V019.Commands.CalloutCOPCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.CalloutToSection_16.png"),
                    ToolTip = "Callout To Section View Placement"
                },
                new PushButtonData("Btn_RefSectionHeadPlacerCommand V013", "Section Head Placer", assemblyPath, "Revit26_Plugin.RefSectionHeadPlacer.V013.Commands.RefSectionHeadPlacerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.RefSectionHeadPlacer_16.png"),
                    ToolTip = "Reference Section Head Placer"
                },
                new PushButtonData("Btn_SectionViewAutoTagger.V004", "Section View Auto Tagger", assemblyPath, "Revit26_Plugin.SectionViewAutoTagger.V004.SectionViewAutoTaggerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.SectionAutoTagger_16.png")
                },
            });

            RibbonPanel renamePanel = app.CreateRibbonPanel(tabName, "View Rename");
            RibbonLayoutHelper.AddStackedButtons(renamePanel, new List<PushButtonData>
            {
                new PushButtonData("Btn_BubbleAutoRenumber_V006", "Bubble Auto Renumber", assemblyPath, "Revit26_Plugin.BubbleAutoRenumber.V006.Commands.SectionAutoRenumberCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.BubbleAutoRenumber_16.png")
                },
                new PushButtonData("Btn_SectionAutoRenamer_V024", "Section Auto Renamer", assemblyPath, "Revit26_Plugin.SectionAutoRenamer.V024.Commands.OpenSectionManagerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.SectionAutoRenamer_16.png")
                },
                // Two coexisting implementations of the same tool — version
                // kept only here so the pair stays distinguishable.
                new PushButtonData("Btn_ViewAutoRenamer_V003", "Auto Renamer (V003)", assemblyPath, "Revit26_Plugin.ViewAutoRenamer.V003.Commands.OpenViewAutoRenamerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.ViewAutoRenamer_16.png"),
                    ToolTip = "View Auto Renamer (V003)"
                },
                new PushButtonData("Btn_ViewAutoRenamer_V004", "View Auto Renamer (V004)", assemblyPath, "Revit26_Plugin.ViewAutoRenamer.V004.Commands.OpenViewAutoRenamerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.ViewAutoRenamer_V004_16.png")
                },
            });
        }
    }
}
