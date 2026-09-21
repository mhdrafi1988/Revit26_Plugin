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

            // Coexisting implementations of the same tool are collected under
            // one pulldown button — no default click, the list always shows.
            // The newest version of each tool supplies the pulldown's own icon.
            // Edge Around is down to its current version (V005); it stays in a
            // pulldown so a future version can be added beside it. Edge Element
            // keeps V002 and V001.
            var edgeAroundV005 = new PushButtonData("Btn_RoofEdgeAroundSections_V005", "Edge Around V005", assemblyPath, "Revit26_Plugin.RoofEdgeAroundSections.V005.RoofEdgeAroundSectionsCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.RoofEdgeAroundSections_V005_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Roof Edge Around Sections", "V005")
            };
            var edgeAroundPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_RoofEdgeAroundSections", "Edge Around", edgeAroundV005);

            var edgeElementV002 = new PushButtonData("Btn_RoofEdgeElementSections_V002", "Edge Element V002", assemblyPath, "Revit26_Plugin.RoofEdgeElementSections.V002.RoofEdgeElementSectionsCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.RoofEdgeElementSections_V002_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Roof Edge Element Sections", "V002")
            };
            var edgeElementV001 = new PushButtonData("Btn_RoofEdgeElementSections_V001", "Edge Element V001", assemblyPath, "Revit26_Plugin.RoofEdgeElementSections.V001.RoofEdgeElementSectionsCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.RoofEdgeElementSections_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Roof Edge Element Sections", "V001")
            };
            var edgeElementPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_RoofEdgeElementSections", "Edge Element", edgeElementV002);

            // Pulldown buttons, unlike split buttons, are fine in a 3-item
            // stack — so this is one call instead of a stack-of-2 + lone item.
            var createItems = RibbonLayoutHelper.AddStackedButtons(createPanel, new List<RibbonItemData>
            {
                new PushButtonData("Btn_ Sections From Detail Lines V11", "Sections From Lines", assemblyPath, "Revit26_Plugin.CreateSections.V011.Commands.CreateSectionsFromDetailLines")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.SectionsFromDetailLines_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Create Sections From Detail Lines", "V011")
                },
                edgeAroundPulldownData,
                edgeElementPulldownData,
            });
            // Fourth item leaves a lone full-size button, so it carries the 32px
            // icon as its LargeImage rather than an upscaled 16px one.
            RibbonLayoutHelper.AddStackedButtons(createPanel, new List<RibbonItemData>
            {
                new PushButtonData("Btn_RoofViewFocus_V001", "Roof View Focus", assemblyPath, "Revit26_Plugin.RoofViewFocus.V001.Commands.RoofViewFocusCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.RoofViewFocus_16.png"),
                    LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.RoofViewFocus_32.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Roof View Focus", "V001", "Crops the active plan view to the selected roofs.")
                },
            });
            RibbonLayoutHelper.WirePulldownButton(createItems, "Pulldown_RoofEdgeAroundSections", edgeAroundV005);
            RibbonLayoutHelper.WirePulldownButton(createItems, "Pulldown_RoofEdgeElementSections", edgeElementV002, edgeElementV001);

            RibbonPanel placePanel = app.CreateRibbonPanel(tabName, "View Place");
            RibbonLayoutHelper.AddStackedButtons(placePanel, new List<RibbonItemData>
            {
                new PushButtonData("Btn_AutoPlaceSectionsCommand_V322_01", "Place Sections", assemblyPath, "Revit26_Plugin.APUS.V322.Commands.AutoPlaceSectionsCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.AutoPlaceSections_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Auto Place Sections", "V322")
                },
                new PushButtonData("Btn_CalloutToSectionViewPlacement_V019", "Callout To Section", assemblyPath, "Revit26_Plugin.CalloutCOP.V019.Commands.CalloutCOPCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.CalloutToSection_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Callout To Section View Placement", "V019")
                },
                new PushButtonData("Btn_RefSectionHeadPlacerCommand V013", "Section Head Placer", assemblyPath, "Revit26_Plugin.RefSectionHeadPlacer.V013.Commands.RefSectionHeadPlacerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.RefSectionHeadPlacer_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Reference Section Head Placer", "V013")
                },
                new PushButtonData("Btn_SectionViewAutoTagger.V004", "Section View Tagger", assemblyPath, "Revit26_Plugin.SectionViewAutoTagger.V004.SectionViewAutoTaggerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.SectionAutoTagger_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Section View Auto Tagger", "V004")
                },
            });

            RibbonPanel renamePanel = app.CreateRibbonPanel(tabName, "View Rename");

            var autoRenamerV004 = new PushButtonData("Btn_ViewAutoRenamer_V004", "View Renamer V004", assemblyPath, "Revit26_Plugin.ViewAutoRenamer.V004.Commands.OpenViewAutoRenamerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.ViewAutoRenamer_V004_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("View Auto Renamer", "V004")
            };
            var autoRenamerPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_ViewAutoRenamer", "Auto Renamer", autoRenamerV004);

            var renameItems = RibbonLayoutHelper.AddStackedButtons(renamePanel, new List<RibbonItemData>
            {
                new PushButtonData("Btn_BubbleAutoRenumber_V006", "Bubble Renumber", assemblyPath, "Revit26_Plugin.BubbleAutoRenumber.V006.Commands.SectionAutoRenumberCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.BubbleAutoRenumber_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Bubble Auto Renumber", "V006")
                },
                new PushButtonData("Btn_SectionAutoRenamer_V024", "Section Renamer", assemblyPath, "Revit26_Plugin.SectionAutoRenamer.V024.Commands.OpenSectionManagerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.SectionAutoRenamer_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Section Auto Renamer", "V024")
                },
                autoRenamerPulldownData,
            });
            RibbonLayoutHelper.WirePulldownButton(renameItems, "Pulldown_ViewAutoRenamer", autoRenamerV004);
        }
    }
}
