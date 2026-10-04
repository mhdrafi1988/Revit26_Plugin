using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
using System.Collections.Generic;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class ViewToolsRibbon
    {
        // Every View Tools button is a pulldown: the current version first, the
        // UI-standard rebuild (Revit_Plugin_UI_Standard.md layout) second.
        private const string Icons = "Revit26_Plugin.Resources.Icons.ViewTools.";
        private const string Rebuilt = " with the standard three-zone window layout.";
        private const string OnShell = " on the shared ToolWindowShell (log in body, capped grids).";

        private static PushButtonData Btn(string assemblyPath, string name, string text, string commandClass,
                                          string icon, string tool, string version, string detail = null)
        {
            return new PushButtonData(name, text, assemblyPath, commandClass)
            {
                Image = ImageUtils.Load(Icons + icon),
                ToolTip = RibbonLayoutHelper.VersionTip(tool, version, detail)
            };
        }

        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel createPanel = app.CreateRibbonPanel(tabName, "View Create");

            var sectionsFromLinesV012 = Btn(assemblyPath, "Btn_SectionsFromDetailLines_V012", "Sections From Lines V012", "Revit26_Plugin.CreateSections.V012.Commands.CreateSectionsFromDetailLines",
                "SectionsFromDetailLines_16.png", "Create Sections From Detail Lines", "V012", "V011" + Rebuilt);
            var sectionsFromLinesV013 = Btn(assemblyPath, "Btn_SectionsFromDetailLines_V013", "Sections From Lines V013", "Revit26_Plugin.CreateSections.V013.Commands.CreateSectionsFromDetailLines",
                "SectionsFromDetailLines_16.png", "Create Sections From Detail Lines", "V013", "V012" + OnShell);

            var edgeAroundV006 = Btn(assemblyPath, "Btn_RoofEdgeAroundSections_V006", "Section Around Edges V006", "Revit26_Plugin.RoofEdgeAroundSections.V006.RoofEdgeAroundSectionsCommand",
                "RoofEdgeAroundSections_V005_16.png", "Roof Edge Around Sections", "V006", "V005" + Rebuilt);
            var edgeAroundV007 = Btn(assemblyPath, "Btn_RoofEdgeAroundSections_V007", "Section Around Edges V007", "Revit26_Plugin.RoofEdgeAroundSections.V007.RoofEdgeAroundSectionsCommand",
                "RoofEdgeAroundSections_V005_16.png", "Roof Edge Around Sections", "V007", "V006" + OnShell);

            var edgeElementV002 = Btn(assemblyPath, "Btn_RoofEdgeElementSections_V002", "Edge Element V002", "Revit26_Plugin.RoofEdgeElementSections.V002.RoofEdgeElementSectionsCommand",
                "RoofEdgeElementSections_V002_16.png", "Roof Edge Element Sections", "V002");
            var edgeElementV003 = Btn(assemblyPath, "Btn_RoofEdgeElementSections_V003", "Edge Element V003", "Revit26_Plugin.RoofEdgeElementSections.V003.RoofEdgeElementSectionsCommand",
                "RoofEdgeElementSections_V002_16.png", "Roof Edge Element Sections", "V003", "V002" + Rebuilt);
            var edgeElementV004 = Btn(assemblyPath, "Btn_RoofEdgeElementSections_V004", "Edge Element V004", "Revit26_Plugin.RoofEdgeElementSections.V004.RoofEdgeElementSectionsCommand",
                "RoofEdgeElementSections_V002_16.png", "Roof Edge Element Sections", "V004", "V003" + OnShell);

            var roofViewFocusV002 = Btn(assemblyPath, "Btn_RoofViewFocus_V002", "Roof View Focus V002", "Revit26_Plugin.RoofViewFocus.V002.Commands.RoofViewFocusCommand",
                "RoofViewFocus_16.png", "Roof View Focus", "V002", "Crops the active plan view to the selected roofs.");
            var roofViewFocusV003 = Btn(assemblyPath, "Btn_RoofViewFocus_V003", "Roof View Focus V003", "Revit26_Plugin.RoofViewFocus.V003.Commands.RoofViewFocusCommand",
                "RoofViewFocus_16.png", "Roof View Focus", "V003", "V002" + Rebuilt);
            var roofViewFocusV004 = Btn(assemblyPath, "Btn_RoofViewFocus_V004", "Roof View Focus V004", "Revit26_Plugin.RoofViewFocus.V004.Commands.RoofViewFocusCommand",
                "RoofViewFocus_16.png", "Roof View Focus", "V004", "V003" + OnShell);

            // Pulldown buttons, unlike split buttons, are fine in a 4-item
            // stack — so this is one call instead of a stack-of-2 + stack-of-2.
            var createItems = RibbonLayoutHelper.AddStackedButtons(createPanel, new List<RibbonItemData>
            {
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_SectionsFromDetailLines", "Sections From Lines", sectionsFromLinesV012),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_RoofEdgeAroundSections", "Section Around Edges", edgeAroundV006),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_RoofEdgeElementSections", "Edge Element", edgeElementV002),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_RoofViewFocus", "Roof View Focus", roofViewFocusV002),
            });
            RibbonLayoutHelper.WirePulldownButton(createItems, "Pulldown_SectionsFromDetailLines", sectionsFromLinesV012, sectionsFromLinesV013);
            RibbonLayoutHelper.WirePulldownButton(createItems, "Pulldown_RoofEdgeAroundSections", edgeAroundV006, edgeAroundV007);
            RibbonLayoutHelper.WirePulldownButton(createItems, "Pulldown_RoofEdgeElementSections", edgeElementV002, edgeElementV003, edgeElementV004);
            RibbonLayoutHelper.WirePulldownButton(createItems, "Pulldown_RoofViewFocus", roofViewFocusV002, roofViewFocusV003, roofViewFocusV004);

            RibbonPanel placePanel = app.CreateRibbonPanel(tabName, "View Place");

            var placeSectionsV323 = Btn(assemblyPath, "Btn_AutoPlaceSectionsCommand_V323", "Place Sections V323", "Revit26_Plugin.APUS.V323.Commands.AutoPlaceSectionsCommand",
                "AutoPlaceSections_16.png", "Auto Place Sections", "V323", "V322" + Rebuilt);
            var placeSectionsV324 = Btn(assemblyPath, "Btn_AutoPlaceSectionsCommand_V324", "Place Sections V324", "Revit26_Plugin.APUS.V324.Commands.AutoPlaceSectionsCommand",
                "AutoPlaceSections_16.png", "Auto Place Sections", "V324", "V323" + OnShell);
            var calloutV019 = Btn(assemblyPath, "Btn_CalloutToSectionViewPlacement_V019", "Callout To Section V019", "Revit26_Plugin.CalloutCOP.V019.Commands.CalloutCOPCommand",
                "CalloutToSection_16.png", "Callout To Section View Placement", "V019");
            var calloutV020 = Btn(assemblyPath, "Btn_CalloutToSectionViewPlacement_V020", "Callout To Section V020", "Revit26_Plugin.CalloutCOP.V020.Commands.CalloutCOPCommand",
                "CalloutToSection_16.png", "Callout To Section View Placement", "V020", "V019" + Rebuilt);
            var headPlacerV013 = Btn(assemblyPath, "Btn_RefSectionHeadPlacerCommand V013", "Section Head Placer V013", "Revit26_Plugin.RefSectionHeadPlacer.V013.Commands.RefSectionHeadPlacerCommand",
                "RefSectionHeadPlacer_16.png", "Reference Section Head Placer", "V013");
            var headPlacerV014 = Btn(assemblyPath, "Btn_RefSectionHeadPlacerCommand_V014", "Section Head Placer V014", "Revit26_Plugin.RefSectionHeadPlacer.V014.Commands.RefSectionHeadPlacerCommand",
                "RefSectionHeadPlacer_16.png", "Reference Section Head Placer", "V014", "V013" + Rebuilt);
            var taggerV005 = Btn(assemblyPath, "Btn_SectionViewAutoTagger.V005", "Section View Tagger V005", "Revit26_Plugin.SectionViewAutoTagger.V005.SectionViewAutoTaggerCommand",
                "SectionAutoTagger_16.png", "Section View Auto Tagger", "V005", "V004" + Rebuilt);
            var taggerV006 = Btn(assemblyPath, "Btn_SectionViewAutoTagger.V006", "Section View Tagger V006", "Revit26_Plugin.SectionViewAutoTagger.V006.SectionViewAutoTaggerCommand",
                "SectionAutoTagger_16.png", "Section View Auto Tagger", "V006", "V005" + OnShell);

            var placeItems = RibbonLayoutHelper.AddStackedButtons(placePanel, new List<RibbonItemData>
            {
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_AutoPlaceSections", "Place Sections", placeSectionsV323),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_CalloutToSection", "Callout To Section", calloutV019),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_RefSectionHeadPlacer", "Section Head Placer", headPlacerV013),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_SectionViewAutoTagger", "Section View Tagger", taggerV005),
            });
            RibbonLayoutHelper.WirePulldownButton(placeItems, "Pulldown_AutoPlaceSections", placeSectionsV323, placeSectionsV324);
            RibbonLayoutHelper.WirePulldownButton(placeItems, "Pulldown_CalloutToSection", calloutV019, calloutV020);
            RibbonLayoutHelper.WirePulldownButton(placeItems, "Pulldown_RefSectionHeadPlacer", headPlacerV013, headPlacerV014);
            RibbonLayoutHelper.WirePulldownButton(placeItems, "Pulldown_SectionViewAutoTagger", taggerV005, taggerV006);

            RibbonPanel renamePanel = app.CreateRibbonPanel(tabName, "View Rename");

            var bubbleV007 = Btn(assemblyPath, "Btn_BubbleAutoRenumber_V007", "Bubble Renumber V007", "Revit26_Plugin.BubbleAutoRenumber.V007.Commands.SectionAutoRenumberCommand",
                "BubbleAutoRenumber_16.png", "Bubble Auto Renumber", "V007", "V006" + Rebuilt);
            var bubbleV008 = Btn(assemblyPath, "Btn_BubbleAutoRenumber_V008", "Bubble Renumber V008", "Revit26_Plugin.BubbleAutoRenumber.V008.Commands.SectionAutoRenumberCommand",
                "BubbleAutoRenumber_16.png", "Bubble Auto Renumber", "V008", "V007" + OnShell);
            var sectionRenamerV025 = Btn(assemblyPath, "Btn_SectionAutoRenamer_V025", "Section Renamer V025", "Revit26_Plugin.SectionAutoRenamer.V025.Commands.OpenSectionManagerCommand",
                "SectionAutoRenamer_16.png", "Section Auto Renamer", "V025", "V024" + Rebuilt);
            var sectionRenamerV026 = Btn(assemblyPath, "Btn_SectionAutoRenamer_V026", "Section Renamer V026", "Revit26_Plugin.SectionAutoRenamer.V026.Commands.OpenSectionManagerCommand",
                "SectionAutoRenamer_16.png", "Section Auto Renamer", "V026", "V025" + OnShell);
            var autoRenamerV005 = Btn(assemblyPath, "Btn_ViewAutoRenamer_V005", "View Renamer V005", "Revit26_Plugin.ViewAutoRenamer.V005.Commands.OpenViewAutoRenamerCommand",
                "ViewAutoRenamer_V004_16.png", "View Auto Renamer", "V005", "V004" + Rebuilt);
            var autoRenamerV006 = Btn(assemblyPath, "Btn_ViewAutoRenamer_V006", "View Renamer V006", "Revit26_Plugin.ViewAutoRenamer.V006.Commands.OpenViewAutoRenamerCommand",
                "ViewAutoRenamer_V004_16.png", "View Auto Renamer", "V006", "V005" + OnShell);

            var renameItems = RibbonLayoutHelper.AddStackedButtons(renamePanel, new List<RibbonItemData>
            {
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_BubbleAutoRenumber", "Bubble Renumber", bubbleV007),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_SectionAutoRenamer", "Section Renamer", sectionRenamerV025),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_ViewAutoRenamer", "Auto Renamer", autoRenamerV005),
            });
            RibbonLayoutHelper.WirePulldownButton(renameItems, "Pulldown_BubbleAutoRenumber", bubbleV007, bubbleV008);
            RibbonLayoutHelper.WirePulldownButton(renameItems, "Pulldown_SectionAutoRenamer", sectionRenamerV025, sectionRenamerV026);
            RibbonLayoutHelper.WirePulldownButton(renameItems, "Pulldown_ViewAutoRenamer", autoRenamerV005, autoRenamerV006);
        }
    }
}
