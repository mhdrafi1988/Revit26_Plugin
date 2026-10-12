using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
using Revit26_Plugin.Shared.Services;
using System.Collections.Generic;

namespace Revit26_Plugin.Menu.Ribbon
{
    /// <summary>
    /// Builds the Annotate and Output panels of the SloperPro Documentation tab. These buttons used
    /// to sit in the Roof Tools, Detail Lines, Manage and Project Tools panels of the single
    /// SloperPro tab; button internal names are unchanged (plugin 4.0.0).
    /// </summary>
    public static class AnnotateOutputRibbon
    {
        private const string Rebuilt = " with the standard three-zone window layout.";

        private static PushButtonData Btn(string assemblyPath, string name, string text, string commandClass,
                                          string icon, string tool, string version, string detail = null)
        {
            return new PushButtonData(name, text, assemblyPath, commandClass)
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons." + icon),
                ToolTip = RibbonLayoutHelper.VersionTip(tool, version, detail)
            };
        }

        /// <summary>Creates the Annotate and Output panels on <paramref name="tabName"/>.</summary>
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            // Annotate: Roof Tag, Line Dims, Overlaps (3-item stack).
            RibbonPanel annotatePanel = app.CreateRibbonPanel(tabName, "Annotate");

            var roofTagV017 = Btn(assemblyPath, "Btn_RoofTagCommand_V017", "Roof Tag V017", "Revit26_Plugin.RoofTag.V017.RoofTagCommand",
                "RoofTools.RoofTag_16.png", "Roof Tag", "V017", "V016" + Rebuilt);
            var roofTagV018 = Btn(assemblyPath, "Btn_RoofTagCommand_V018", "Roof Tag V018", "Revit26_Plugin.RoofTag.V018.RoofTagCommand",
                "RoofTools.RoofTag_16.png", "Roof Tag", "V018", "V017 on the shared ToolWindowShell (log in body).");

            var lineDimsPulldownData = DimensionsRibbon.CreateDetailLineDimPulldown(assemblyPath, out var lineDimsVersions);

            var overlapV003 = Btn(assemblyPath, "Btn_AnnotationOverlapDetection_V003", "Overlap Detection V003", "Revit26_Plugin.AnnotationOverlapDetection.V003.Command",
                "Manage.AnnotationOverlapDetection_16.png", "Annotation Overlap Detection", "V003", "V002" + Rebuilt);
            var overlapV004 = Btn(assemblyPath, "Btn_AnnotationOverlapDetection_V004", "Overlap Detection V004", "Revit26_Plugin.AnnotationOverlapDetection.V004.Command",
                "Manage.AnnotationOverlapDetection_16.png", "Annotation Overlap Detection", "V004", "V003 on the shared ToolWindowShell (capped lists).");

            var annotateItems = RibbonLayoutHelper.AddStackedButtons(annotatePanel, new List<RibbonItemData>
            {
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_RoofTag", "Roof Tag", roofTagV018),
                lineDimsPulldownData,
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_AnnotationOverlapDetection", "Overlaps", overlapV004),
            });
            RibbonLayoutHelper.WirePulldownButton(annotateItems, "Pulldown_RoofTag", roofTagV018, roofTagV017);
            RibbonLayoutHelper.WirePulldownButton(annotateItems, DimensionsRibbon.DetailLineDimPulldownName, lineDimsVersions);
            RibbonLayoutHelper.WirePulldownButton(annotateItems, "Pulldown_AnnotationOverlapDetection", overlapV004, overlapV003);

            // Output: Schedule I/O and Export DWG.
            RibbonPanel outputPanel = app.CreateRibbonPanel(tabName, "Output");

            // Schedule Export/Import — latest two versions: V007 (ToolWindowShell) first,
            // then V006 (its three-zone-layout predecessor).
            var scheduleExportImportV006 = Btn(assemblyPath, "Btn_ScheduleExportImport_V006", "Schedule Export/Import V006", "Revit26_Plugin.ScheduleExportImport.V006.Commands.ScheduleExportImportCommand",
                "Manage.ScheduleExportImport_16.png", "Schedule Export / Import", "V006", "V005" + Rebuilt);
            var scheduleExportImportV007 = Btn(assemblyPath, "Btn_ScheduleExportImport_V007", "Schedule Export/Import V007", "Revit26_Plugin.ScheduleExportImport.V007.Commands.ScheduleExportImportCommand",
                "Manage.ScheduleExportImport_16.png", "Schedule Export / Import", "V007", "V006 on the shared ToolWindowShell (log in body, capped grids).");

            var exportDwgV001 = new PushButtonData("Btn_ExportDwgToFolder_V001", "Export DWG V001", assemblyPath, "Revit26_Plugin.ExportDwgToFolder.V001.Commands.ExportDwgToFolderCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.DwgToDetailLines_V011_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Export DWG to Folder", "V001", "Export all embedded CAD links and imports to a folder, organised by source type and view category.")
            };

            var outputItems = RibbonLayoutHelper.AddStackedButtons(outputPanel, new List<RibbonItemData>
            {
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_ScheduleExportImport", "Schedule I/O", scheduleExportImportV007),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_ExportDwgToFolder", "Export DWG", exportDwgV001),
            });
            RibbonLayoutHelper.WirePulldownButton(outputItems, "Pulldown_ScheduleExportImport", scheduleExportImportV007, scheduleExportImportV006);
            RibbonLayoutHelper.WirePulldownButton(outputItems, "Pulldown_ExportDwgToFolder", exportDwgV001);
        }
    }
}
