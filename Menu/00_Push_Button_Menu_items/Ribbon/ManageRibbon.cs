using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
using System.Collections.Generic;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class ManageRibbon
    {
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = app.CreateRibbonPanel(tabName, "Manage");

            var paraManagerV003 = new PushButtonData("Btn_ParaManager_V003", "ParaManager", assemblyPath, "Revit26_Plugin.ParaManager.V003.ParaManagerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Manage.ParaManager_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("ParaManager", "V003",
                    "Bulk-assign shared parameters from a shared parameter file to categories, Instance or Type bound, via a guided step-by-step flow.")
            };

            // Schedule Export/Import — every working version in one dropdown, newest first.
            // V001 is left out: its export can never complete (log-scroll bug fixed in V002).
            var scheduleExportImportV005 = new PushButtonData("Btn_ScheduleExportImport_V005", "Schedule Export/Import V005", assemblyPath, "Revit26_Plugin.ScheduleExportImport.V005.Commands.ScheduleExportImportCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Manage.ScheduleExportImport_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Schedule Export / Import", "V005",
                    "Flags values changed in Revit since export, checks element ownership, compares numbers by value, Excel dropdowns, type-parameter edits, hidden fields, remembers the folder.")
            };
            var scheduleExportImportV004 = new PushButtonData("Btn_ScheduleExportImport_V004", "Schedule Export/Import V004", assemblyPath, "Revit26_Plugin.ScheduleExportImport.V004.Commands.ScheduleExportImportCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Manage.ScheduleExportImport_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Schedule Export / Import", "V004",
                    "Export / Import buttons in a row under the metric cards; schedule list full width. Everything from V003.")
            };
            var scheduleExportImportV003 = new PushButtonData("Btn_ScheduleExportImport_V003", "Schedule Export/Import V003", assemblyPath, "Revit26_Plugin.ScheduleExportImport.V003.Commands.ScheduleExportImportCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Manage.ScheduleExportImport_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Schedule Export / Import", "V003",
                    "Errors always shown, import picks the schedule from the file, explains 'nothing to import', Base Level by name.")
            };
            var scheduleExportImportV002 = new PushButtonData("Btn_ScheduleExportImport_V002", "Schedule Export/Import V002", assemblyPath, "Revit26_Plugin.ScheduleExportImport.V002.Commands.ScheduleExportImportCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Manage.ScheduleExportImport_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Schedule Export / Import", "V002",
                    "Export to Excel and import back by Element ID; works with the workbook still open in Excel.")
            };
            var scheduleExportImportPulldownData = RibbonLayoutHelper.CreatePulldownButtonData(
                "Pulldown_ScheduleExportImport", "Schedule Export/Import", scheduleExportImportV005);

            var manageItems = RibbonLayoutHelper.AddStackedButtons(panel, new List<RibbonItemData>
            {
                new PushButtonData("Btn_AnnotationOverlapDetection_V002", "Overlap Detection", assemblyPath, "Revit26_Plugin.AnnotationOverlapDetection.V002.Command")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Manage.AnnotationOverlapDetection_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Annotation Overlap Detection", "V002")
                },
                paraManagerV003,
                new PushButtonData("Btn_WorksetsElementsBrowser_WSEB002", "Worksets & Elements", assemblyPath, "Revit26_Plugin.WorksetsElementsBrowser.WSEB002.Commands.WorksetsElementsBrowserCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Manage.WorksetsElementsBrowser_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Worksets & Elements Browser", "WSEB002",
                        "Browse worksets, categories and types in one checkbox tree; isolate or select checked elements in a chosen 3D view.")
                },
                new PushButtonData("Btn_WorksetManager_V012_New", "Workset Manager", assemblyPath, "Revit26_Plugin.WorksetManager.V012.Commands.WorksetManagerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.WorksetManager_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Workset Manager", "V012")
                },
new PushButtonData("Btn_WorksetRenamer_V003", "Workset Renamer", assemblyPath, "Revit26_Plugin.WorksetRenamer.V003.Command")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.WorksetRename_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Workset Renamer", "V003")
                },
                new PushButtonData("Btn_WorksetRenamer_FX03", "Workset Renamer (Excel)", assemblyPath, "Revit26_Plugin.WorksetRenamer.FX03.Command")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.WorksetRename_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Workset Renamer (From Excel)", "FX03")
                },
                scheduleExportImportPulldownData,
            });
            RibbonLayoutHelper.WirePulldownButton(manageItems, "Pulldown_ScheduleExportImport",
                scheduleExportImportV005, scheduleExportImportV004, scheduleExportImportV003, scheduleExportImportV002);
        }
    }
}
