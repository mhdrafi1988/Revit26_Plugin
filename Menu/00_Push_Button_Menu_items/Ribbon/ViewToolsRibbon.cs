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

            // Two coexisting implementations of the same tool — one split
            // button instead of two stacked entries: one click runs V004
            // (the newer build), the dropdown arrow reveals V003.
            var edgeAroundV004 = new PushButtonData("Btn_RoofEdgeAroundSections_V004", "Edge Around (V004)", assemblyPath, "Revit26_Plugin.RoofEdgeAroundSections.V004.RoofEdgeAroundSectionsCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.RoofEdgeAroundSections_V004_16.png"),
                ToolTip = "Roof Edge Around Sections (V004)"
            };
            var edgeAroundV003 = new PushButtonData("Btn_RoofEdgeAroundSections_V003", "Edge Around (V003)", assemblyPath, "Revit26_Plugin.RoofEdgeAroundSections.V003.RoofEdgeAroundSectionsCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.RoofEdgeAroundSections_16.png"),
                ToolTip = "Roof Edge Around Sections (V003)"
            };
            var edgeAroundSplitData = RibbonLayoutHelper.CreateSplitButtonData("Split_RoofEdgeAroundSections", "Edge Around", edgeAroundV004);

            // A split/pulldown button can only sit in a stack of exactly 2 —
            // Revit doesn't render the dropdown affordance in a stack of 3
            // (falls back to something broken instead). So this is two calls:
            // a 2-item stack (push + split), then the last button on its own.
            var createItems = RibbonLayoutHelper.AddStackedButtons(createPanel, new List<RibbonItemData>
            {
                new PushButtonData("Btn_ Sections From Detail Lines V11", "Sections From Lines", assemblyPath, "Revit26_Plugin.CreateSections.V011.Commands.CreateSectionsFromDetailLines")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.SectionsFromDetailLines_16.png"),
                    ToolTip = "Create Sections From Detail Lines"
                },
                edgeAroundSplitData,
            });
            RibbonLayoutHelper.WireSplitButton(createItems, "Split_RoofEdgeAroundSections", edgeAroundV004, edgeAroundV003);

            RibbonLayoutHelper.AddStackedButtons(createPanel, new List<RibbonItemData>
            {
                new PushButtonData("Btn_RoofEdgeElementSections_V001", "Roof Edge Sections", assemblyPath, "Revit26_Plugin.RoofEdgeElementSections.V001.RoofEdgeElementSectionsCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.RoofEdgeElementSections_16.png"),
                    ToolTip = "Roof Edge Element Sections"
                },
            });

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

            var autoRenamerV004 = new PushButtonData("Btn_ViewAutoRenamer_V004", "View Renamer (V004)", assemblyPath, "Revit26_Plugin.ViewAutoRenamer.V004.Commands.OpenViewAutoRenamerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.ViewAutoRenamer_V004_16.png"),
                ToolTip = "View Auto Renamer (V004)"
            };
            var autoRenamerV003 = new PushButtonData("Btn_ViewAutoRenamer_V003", "Renamer (V003)", assemblyPath, "Revit26_Plugin.ViewAutoRenamer.V003.Commands.OpenViewAutoRenamerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.ViewTools.ViewAutoRenamer_16.png"),
                ToolTip = "View Auto Renamer (V003)"
            };
            var autoRenamerSplitData = RibbonLayoutHelper.CreateSplitButtonData("Split_ViewAutoRenamer", "Auto Renamer", autoRenamerV004);

            // Same stack-of-3 restriction as View Create: the split button can't
            // share a 3-item stack, so it's on its own after a plain 2-item stack.
            RibbonLayoutHelper.AddStackedButtons(renamePanel, new List<RibbonItemData>
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
            });

            var renameItems = RibbonLayoutHelper.AddStackedButtons(renamePanel, new List<RibbonItemData> { autoRenamerSplitData });
            RibbonLayoutHelper.WireSplitButton(renameItems, "Split_ViewAutoRenamer", autoRenamerV004, autoRenamerV003);
        }
    }
}
