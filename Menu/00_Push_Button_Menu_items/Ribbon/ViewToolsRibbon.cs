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

            // Coexisting implementations of the same tool, collected under
            // one pulldown button — no default click, the list always shows.
            // The newest version of each tool (Edge Around V005, Edge Element V002)
            // supplies the pulldown's own icon; the older version also gets one.
            var edgeAroundV005 = new PushButtonData("Btn_RoofEdgeAroundSections_V005", "Edge Around V005", assemblyPath, "Revit26_Plugin.RoofEdgeAroundSections.V005.RoofEdgeAroundSectionsCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.RoofEdgeAroundSections_V005_16.png"),
                ToolTip = "Roof Edge Around Sections (V005)"
            };
            var edgeAroundV004 = new PushButtonData("Btn_RoofEdgeAroundSections_V004", "Edge Around V004", assemblyPath, "Revit26_Plugin.RoofEdgeAroundSections.V004.RoofEdgeAroundSectionsCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.RoofEdgeAroundSections_V004_16.png"),
                ToolTip = "Roof Edge Around Sections (V004)"
            };
            var edgeAroundPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_RoofEdgeAroundSections", "Edge Around", edgeAroundV005);

            var edgeElementV002 = new PushButtonData("Btn_RoofEdgeElementSections_V002", "Edge Element V002", assemblyPath, "Revit26_Plugin.RoofEdgeElementSections.V002.RoofEdgeElementSectionsCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.RoofEdgeElementSections_V002_16.png"),
                ToolTip = "Roof Edge Element Sections (V002)"
            };
            var edgeElementV001 = new PushButtonData("Btn_RoofEdgeElementSections_V001", "Edge Element V001", assemblyPath, "Revit26_Plugin.RoofEdgeElementSections.V001.RoofEdgeElementSectionsCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.RoofEdgeElementSections_16.png"),
                ToolTip = "Roof Edge Element Sections (V001)"
            };
            var edgeElementPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_RoofEdgeElementSections", "Edge Element", edgeElementV002);

            // Pulldown buttons, unlike split buttons, are fine in a 3-item
            // stack — so this is one call instead of a stack-of-2 + lone item.
            var createItems = RibbonLayoutHelper.AddStackedButtons(createPanel, new List<RibbonItemData>
            {
                new PushButtonData("Btn_ Sections From Detail Lines V11", "Sections From Lines", assemblyPath, "Revit26_Plugin.CreateSections.V011.Commands.CreateSectionsFromDetailLines")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.SectionsFromDetailLines_16.png"),
                    ToolTip = "Create Sections From Detail Lines"
                },
                edgeAroundPulldownData,
                edgeElementPulldownData,
            });
            RibbonLayoutHelper.WirePulldownButton(createItems, "Pulldown_RoofEdgeAroundSections", edgeAroundV005, edgeAroundV004);
            RibbonLayoutHelper.WirePulldownButton(createItems, "Pulldown_RoofEdgeElementSections", edgeElementV002, edgeElementV001);

            RibbonPanel placePanel = app.CreateRibbonPanel(tabName, "View Place");
            RibbonLayoutHelper.AddStackedButtons(placePanel, new List<RibbonItemData>
            {
                new PushButtonData("Btn_AutoPlaceSectionsCommand_V322_01", "Place Sections", assemblyPath, "Revit26_Plugin.APUS.V322.Commands.AutoPlaceSectionsCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.AutoPlaceSections_16.png"),
                    ToolTip = "Auto Place Sections"
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
                new PushButtonData("Btn_SectionViewAutoTagger.V004", "Section View Tagger", assemblyPath, "Revit26_Plugin.SectionViewAutoTagger.V004.SectionViewAutoTaggerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.SectionAutoTagger_16.png"),
                    ToolTip = "Section View Auto Tagger"
                },
            });

            RibbonPanel renamePanel = app.CreateRibbonPanel(tabName, "View Rename");

            var autoRenamerV004 = new PushButtonData("Btn_ViewAutoRenamer_V004", "View Renamer V004", assemblyPath, "Revit26_Plugin.ViewAutoRenamer.V004.Commands.OpenViewAutoRenamerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.ViewAutoRenamer_V004_16.png"),
                ToolTip = "View Auto Renamer (V004)"
            };
            var autoRenamerPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_ViewAutoRenamer", "Auto Renamer", autoRenamerV004);

            var renameItems = RibbonLayoutHelper.AddStackedButtons(renamePanel, new List<RibbonItemData>
            {
                new PushButtonData("Btn_BubbleAutoRenumber_V006", "Bubble Renumber", assemblyPath, "Revit26_Plugin.BubbleAutoRenumber.V006.Commands.SectionAutoRenumberCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.BubbleAutoRenumber_16.png"),
                    ToolTip = "Bubble Auto Renumber"
                },
                new PushButtonData("Btn_SectionAutoRenamer_V024", "Section Renamer", assemblyPath, "Revit26_Plugin.SectionAutoRenamer.V024.Commands.OpenSectionManagerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.SectionAutoRenamer_16.png"),
                    ToolTip = "Section Auto Renamer"
                },
                autoRenamerPulldownData,
            });
            RibbonLayoutHelper.WirePulldownButton(renameItems, "Pulldown_ViewAutoRenamer", autoRenamerV004);
        }
    }
}
