using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.Shared.Services
{
    /// <summary>
    /// Name and version of one ribbon tool.
    /// </summary>
    public sealed class ToolInfo
    {
        /// <summary>Display name, used in tooltips, window titles and error dialogs.</summary>
        public string Name { get; }

        /// <summary>Semantic version (MAJOR.MINOR.PATCH).</summary>
        public string Version { get; }

        /// <summary>Legacy build tag the tool's folder and namespace carry (e.g. V004).</summary>
        public string BuildTag { get; }

        /// <summary>Root namespace of the tool's code; every type under it belongs to this tool.</summary>
        public string NamespacePrefix { get; }

        internal ToolInfo(string name, string version, string buildTag, string namespacePrefix)
        {
            Name = name;
            Version = version;
            BuildTag = buildTag;
            NamespacePrefix = namespacePrefix;
        }

        /// <summary>"Name — vX.Y.Z", used as the tooltip title line and window-title suffix.</summary>
        public string Title => $"{Name} — v{Version}";

        /// <summary>
        /// Ribbon tooltip: the "Name — vX.Y.Z (build Vnnn)" title line, then an optional description.
        /// </summary>
        public string Tip(string detail = null)
        {
            string title = $"{Title} (build {BuildTag})";
            return string.IsNullOrWhiteSpace(detail) ? title : title + "\n" + detail;
        }
    }

    /// <summary>
    /// Single source of truth for every tool's name and semantic version.
    /// Bump a tool's version here (and in CHANGELOG.md) whenever its behaviour changes;
    /// the ribbon tooltip, window title and error dialogs all read from this list.
    /// </summary>
    public static class ToolCatalog
    {
        private static readonly List<ToolInfo> All = new();

        public static readonly ToolInfo LinkedDetailLineGenerator = Add("Detail Lines From Links", "7.0.0", "VA007", "Revit26_Plugin.LinkedDetailLineGenerator.VA007");
        public static readonly ToolInfo DetailLineClosedLoop = Add("Detail Line Closed Loop", "1.0.0", "V001", "Revit26_Plugin.DetailLineClosedLoop.V001");
        public static readonly ToolInfo DtlLineDim = Add("Auto Dim Detail Line", "8.0.0", "V008", "Revit26_Plugin.DtlLineDim.V008");
        public static readonly ToolInfo FloorsAndRoofFromLinkedRooms = Add("Floors And Roof From Linked Rooms", "11.0.0", "V011", "Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V011");
        public static readonly ToolInfo FloorsAndRoofFromLinkedRoomsViaPlanView = Add("Floors And Roof From Linked Rooms (Via Plan View)", "4.0.0", "V004", "Revit26_Plugin.FloorsAndRoofFromLinkedRoomsViaPlanView.V004");
        public static readonly ToolInfo ParaManager = Add("ParaManager", "3.0.0", "V003", "Revit26_Plugin.ParaManager.V003");
        public static readonly ToolInfo ScheduleExportImport = Add("Schedule Export / Import", "4.0.0", "V004", "Revit26_Plugin.ScheduleExportImport.V004");
        public static readonly ToolInfo AnnotationOverlapDetection = Add("Annotation Overlap Detection", "2.0.0", "V002", "Revit26_Plugin.AnnotationOverlapDetection.V002");
        public static readonly ToolInfo WorksetsElementsBrowser = Add("Worksets & Elements Browser", "2.0.0", "WSEB002", "Revit26_Plugin.WorksetsElementsBrowser.WSEB002");
        public static readonly ToolInfo WorksetManager = Add("Workset Manager", "12.0.0", "V012", "Revit26_Plugin.WorksetManager.V012");
        public static readonly ToolInfo WorksetRenamer = Add("Workset Renamer", "3.0.0", "V003", "Revit26_Plugin.WorksetRenamer.V003");
        public static readonly ToolInfo WorksetRenamerExcel = Add("Workset Renamer (From Excel)", "3.0.0", "FX03", "Revit26_Plugin.WorksetRenamer.FX03");
        public static readonly ToolInfo AutoSlopeByPoint = Add("Auto Slope By Point", "28.0.0", "V028", "Revit26_Plugin.AutoSlopeByPoint.V028");
        public static readonly ToolInfo AutoSlopeByPointRidge = Add("Auto Slope By Point (Ridge)", "1.0.0", "V001", "Revit26_Plugin.AutoSlopeByPointRidge.V001");
        public static readonly ToolInfo AutoSlopeByPointMultiCopies = Add("Auto Slope By Point (Multi Copies)", "28.0.0", "MultiCopies28", "Revit26_Plugin.AutoSlopeByPoint.MultiCopies28");
        public static readonly ToolInfo AutoSlopeByDrain = Add("Auto Slope By Drain", "7.0.0", "V007", "Revit26_Plugin.AutoSlopeByDrain.V007");
        public static readonly ToolInfo MultiRoofSlopeByDrain = Add("Auto Slope By Drain (Multi-Roof)", "10.0.0", "V010", "Revit26_Plugin.MultiRoofSlopeByDrain.V010");
        public static readonly ToolInfo InnerLoopDivider = Add("Divide Inner Loops", "9.0.0", "V009", "Revit26_Plugin.InnerLoopDivider.V009");
        public static readonly ToolInfo OuterCurveDivider = Add("Outer Curve Divider", "4.0.0", "V004", "Revit26_Plugin.OuterCurveDivider.V004");
        public static readonly ToolInfo RoofDetailLineIntersect = Add("Roof Detail Line Intersect", "12.0.0", "V012", "Revit26_Plugin.RoofDetailLineIntersect.V012");
        public static readonly ToolInfo InnerLoopsAndPerpendicular = Add("Inner Loops And Perpendicular", "5.0.0", "V005", "Revit26_Plugin.InnerLoopsAndPerpendicular.V005");
        public static readonly ToolInfo RoofEdgeVertexReducer = Add("Roof Edge Vertex Reducer", "7.0.0", "V007", "Revit26_Plugin.RoofEdgeVertexReducer.V007");
        public static readonly ToolInfo MultiplePoints = Add("Multiple Points (1/2–1/4)", "1.0.0", "V001", "Revit26_Plugin.MultiplePoints.V001");
        public static readonly ToolInfo RidgeByOpenings = Add("Ridge By Openings", "68.0.0", "V068", "Revit26_Plugin.RoofRidgeLines.V068");
        public static readonly ToolInfo RidgeByPoints = Add("Ridge By Points", "57.0.0", "V057", "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V057");
        public static readonly ToolInfo CreaserAdv = Add("Creaser Adv", "10.0.0", "V010", "Revit26_Plugin.CreaserAdv.V010");
        public static readonly ToolInfo RoofTag = Add("Roof Tag", "16.0.0", "V016", "Revit26_Plugin.RoofTag.V016");
        public static readonly ToolInfo RoofFromDetailLines = Add("Roof From Detail Lines", "7.0.0", "V007", "Revit26_Plugin.RoofFromDetailLines.V007");
        public static readonly ToolInfo CombinedRoofTools = Add("Combined Roof Tools", "1.0.0", "V001", "Revit26_Plugin.CombinedRoofTools.V001");
        public static readonly ToolInfo RoofTypeManager = Add("Roof Type Manager", "1.0.0", "V001", "Revit26_Plugin.RoofTypeCreator.V001");
        public static readonly ToolInfo RoofPointElevationSync = Add("Roof Point Elevation Sync", "3.0.0", "V003", "Revit26_Plugin.RoofPointElevationSync.V003");
        public static readonly ToolInfo RoofPointComparison = Add("Roof Point Comparison", "1.0.0", "V001", "Revit26_Plugin.RoofPointComparison.V001");
        public static readonly ToolInfo BatchDwgFamilyLinker = Add("Batch Link DWG Family", "0.1.0", "Working build", "BatchDwgFamilyLinker");
        public static readonly ToolInfo DwgToLines = Add("DWG To Lines", "5.0.0", "V005", "Revit26_Plugin.DwgToLines.V005");
        public static readonly ToolInfo DwgToDetailLines = Add("DWG To Detail Lines", "11.0.0", "V011", "Revit26_Plugin.DwgToDetailLines.V011");
        public static readonly ToolInfo ExportDwgToFolder = Add("Export DWG to Folder", "1.0.0", "V001", "Revit26_Plugin.ExportDwgToFolder.V001");
        public static readonly ToolInfo PlanFromScopeBox = Add("Plan From Scope Box", "4.0.0", "V004", "Revit26_Plugin.PlanFromScopeBox.V004");
        public static readonly ToolInfo SmartViewToSheetPlacer = Add("Smart View To Sheet Placer", "222.0.0", "V222", "Revit26_Plugin.SmartViewToSheetPlacer.V222");
        public static readonly ToolInfo AutomatedSectionPlacer = Add("Automated Section Placer", "1.0.0", "V001", "Revit26_Plugin.AutomatedSectionPlacer.V001");
        public static readonly ToolInfo SheetAutoRearrange = Add("Sheet Auto Rearrange", "26.0.0", "V026", "Revit26_Plugin.SheetAutoRearrange.V026");
        public static readonly ToolInfo SheetViewArrange = Add("Sheet View Arrange", "1.0.0", "V001", "Revit26_Plugin.SheetViewArrange.V001");
        public static readonly ToolInfo RoofEdgeAroundSections = Add("Roof Edge Around Sections", "5.0.0", "V005", "Revit26_Plugin.RoofEdgeAroundSections.V005");
        public static readonly ToolInfo RoofEdgeElementSections = Add("Roof Edge Element Sections", "2.0.0", "V002", "Revit26_Plugin.RoofEdgeElementSections.V002");
        public static readonly ToolInfo RoofViewFocus = Add("Roof View Focus", "2.0.0", "V002", "Revit26_Plugin.RoofViewFocus.V002");
        public static readonly ToolInfo CreateSections = Add("Create Sections From Detail Lines", "11.0.0", "V011", "Revit26_Plugin.CreateSections.V011");
        public static readonly ToolInfo APUS = Add("Auto Place Sections", "322.0.0", "V322", "Revit26_Plugin.APUS.V322");
        public static readonly ToolInfo CalloutCOP = Add("Callout To Section View Placement", "19.0.0", "V019", "Revit26_Plugin.CalloutCOP.V019");
        public static readonly ToolInfo RefSectionHeadPlacer = Add("Reference Section Head Placer", "13.0.0", "V013", "Revit26_Plugin.RefSectionHeadPlacer.V013");
        public static readonly ToolInfo SectionViewAutoTagger = Add("Section View Auto Tagger", "4.0.0", "V004", "Revit26_Plugin.SectionViewAutoTagger.V004");
        public static readonly ToolInfo ViewAutoRenamer = Add("View Auto Renamer", "4.0.0", "V004", "Revit26_Plugin.ViewAutoRenamer.V004");
        public static readonly ToolInfo BubbleAutoRenumber = Add("Bubble Auto Renumber", "6.0.0", "V006", "Revit26_Plugin.BubbleAutoRenumber.V006");
        public static readonly ToolInfo SectionAutoRenamer = Add("Section Auto Renamer", "24.0.0", "V024", "Revit26_Plugin.SectionAutoRenamer.V024");
        public static readonly ToolInfo DeleteWorkset = Add("Delete Workset", "1.0.3", "V001", "Revit26_Plugin.DeleteWorkset.V001");
        public static readonly ToolInfo DeleteLineStyles = Add("Delete Line Styles", "1.0.0", "V001", "Revit26_Plugin.DeleteLineStyles.V001");

        private static ToolInfo Add(string name, string version, string buildTag, string namespacePrefix)
        {
            var info = new ToolInfo(name, version, buildTag, namespacePrefix);
            All.Add(info);
            return info;
        }

        /// <summary>
        /// Finds the tool that owns <paramref name="type"/> by namespace, or null for shared/ribbon code.
        /// </summary>
        public static ToolInfo Find(Type type)
        {
            string ns = type?.Namespace;
            if (string.IsNullOrEmpty(ns)) return null;
            return All
                .Where(t => ns == t.NamespacePrefix || ns.StartsWith(t.NamespacePrefix + ".", StringComparison.Ordinal))
                .OrderByDescending(t => t.NamespacePrefix.Length)
                .FirstOrDefault();
        }
    }
}
