using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
using System.Collections.Generic;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class ManageRibbon
    {
        // Every Manage button is a pulldown: the current version first, the
        // UI-standard rebuild (Revit_Plugin_UI_Standard.md layout) second.
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

        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = app.CreateRibbonPanel(tabName, "Manage");

            var overlapV003 = Btn(assemblyPath, "Btn_AnnotationOverlapDetection_V003", "Overlap Detection V003", "Revit26_Plugin.AnnotationOverlapDetection.V003.Command",
                "Manage.AnnotationOverlapDetection_16.png", "Annotation Overlap Detection", "V003", "V002" + Rebuilt);
            var overlapV004 = Btn(assemblyPath, "Btn_AnnotationOverlapDetection_V004", "Overlap Detection V004", "Revit26_Plugin.AnnotationOverlapDetection.V004.Command",
                "Manage.AnnotationOverlapDetection_16.png", "Annotation Overlap Detection", "V004", "V003 on the shared ToolWindowShell (capped lists).");

            const string paraManagerDetail =
                "Bulk-assign shared parameters from a shared parameter file to categories, Instance or Type bound, via a guided step-by-step flow.";
            var paraManagerV003 = Btn(assemblyPath, "Btn_ParaManager_V003", "ParaManager V003", "Revit26_Plugin.ParaManager.V003.ParaManagerCommand",
                "Manage.ParaManager_16.png", "ParaManager", "V003", paraManagerDetail);
            var paraManagerV004 = Btn(assemblyPath, "Btn_ParaManager_V004", "ParaManager V004", "Revit26_Plugin.ParaManager.V004.ParaManagerCommand",
                "Manage.ParaManager_16.png", "ParaManager", "V004", "V003" + Rebuilt);

            var wsebV003 = Btn(assemblyPath, "Btn_WorksetsElementsBrowser_WSEB003", "Worksets & Elements WSEB003", "Revit26_Plugin.WorksetsElementsBrowser.WSEB003.Commands.WorksetsElementsBrowserCommand",
                "Manage.WorksetsElementsBrowser_16.png", "Worksets & Elements Browser", "WSEB003", "WSEB002" + Rebuilt);
            var wsebV004 = Btn(assemblyPath, "Btn_WorksetsElementsBrowser_WSEB004", "Worksets & Elements WSEB004", "Revit26_Plugin.WorksetsElementsBrowser.WSEB004.Commands.WorksetsElementsBrowserCommand",
                "Manage.WorksetsElementsBrowser_16.png", "Worksets & Elements Browser", "WSEB004", "WSEB003 on the shared ToolWindowShell (capped tree).");

            var worksetManagerV012 = Btn(assemblyPath, "Btn_WorksetManager_V012_New", "Workset Manager V012", "Revit26_Plugin.WorksetManager.V012.Commands.WorksetManagerCommand",
                "SetupTools.WorksetManager_16.png", "Workset Manager", "V012");
            var worksetManagerV013 = Btn(assemblyPath, "Btn_WorksetManager_V013", "Workset Manager V013", "Revit26_Plugin.WorksetManager.V013.Commands.WorksetManagerCommand",
                "SetupTools.WorksetManager_16.png", "Workset Manager", "V013", "V012" + Rebuilt);

            var worksetRenamerV004 = Btn(assemblyPath, "Btn_WorksetRenamer_V004", "Workset Renamer V004", "Revit26_Plugin.WorksetRenamer.V004.Command",
                "SetupTools.WorksetRename_16.png", "Workset Renamer", "V004", "V003" + Rebuilt);
            var worksetRenamerV005 = Btn(assemblyPath, "Btn_WorksetRenamer_V005", "Workset Renamer V005", "Revit26_Plugin.WorksetRenamer.V005.Command",
                "SetupTools.WorksetRename_16.png", "Workset Renamer", "V005", "V004 on the shared ToolWindowShell (capped grid).");

            var worksetRenamerFx04 = Btn(assemblyPath, "Btn_WorksetRenamer_FX04", "Workset Renamer (Excel) FX04", "Revit26_Plugin.WorksetRenamer.FX04.Command",
                "SetupTools.WorksetRename_16.png", "Workset Renamer (From Excel)", "FX04", "FX03" + Rebuilt);
            var worksetRenamerFx05 = Btn(assemblyPath, "Btn_WorksetRenamer_FX05", "Workset Renamer (Excel) FX05", "Revit26_Plugin.WorksetRenamer.FX05.Command",
                "SetupTools.WorksetRename_16.png", "Workset Renamer (Excel)", "FX05", "FX04 on the shared ToolWindowShell (capped group grids, shared group colours).");

            // Schedule Export/Import — every working version in one dropdown: the current
            // version (V005) first, its rebuild (V006) second, then the older versions.
            // V001 is left out: its export can never complete (log-scroll bug fixed in V002).
            var scheduleExportImportV006 = Btn(assemblyPath, "Btn_ScheduleExportImport_V006", "Schedule Export/Import V006", "Revit26_Plugin.ScheduleExportImport.V006.Commands.ScheduleExportImportCommand",
                "Manage.ScheduleExportImport_16.png", "Schedule Export / Import", "V006", "V005" + Rebuilt);
            var scheduleExportImportV005 = Btn(assemblyPath, "Btn_ScheduleExportImport_V005", "Schedule Export/Import V005", "Revit26_Plugin.ScheduleExportImport.V005.Commands.ScheduleExportImportCommand",
                "Manage.ScheduleExportImport_16.png", "Schedule Export / Import", "V005",
                "Flags values changed in Revit since export, checks element ownership, compares numbers by value, Excel dropdowns, type-parameter edits, hidden fields, remembers the folder.");
            var scheduleExportImportV004 = Btn(assemblyPath, "Btn_ScheduleExportImport_V004", "Schedule Export/Import V004", "Revit26_Plugin.ScheduleExportImport.V004.Commands.ScheduleExportImportCommand",
                "Manage.ScheduleExportImport_16.png", "Schedule Export / Import", "V004",
                "Export / Import buttons in a row under the metric cards; schedule list full width. Everything from V003.");
            var scheduleExportImportV003 = Btn(assemblyPath, "Btn_ScheduleExportImport_V003", "Schedule Export/Import V003", "Revit26_Plugin.ScheduleExportImport.V003.Commands.ScheduleExportImportCommand",
                "Manage.ScheduleExportImport_16.png", "Schedule Export / Import", "V003",
                "Errors always shown, import picks the schedule from the file, explains 'nothing to import', Base Level by name.");
            var scheduleExportImportV002 = Btn(assemblyPath, "Btn_ScheduleExportImport_V002", "Schedule Export/Import V002", "Revit26_Plugin.ScheduleExportImport.V002.Commands.ScheduleExportImportCommand",
                "Manage.ScheduleExportImport_16.png", "Schedule Export / Import", "V002",
                "Export to Excel and import back by Element ID; works with the workbook still open in Excel.");

            var manageItems = RibbonLayoutHelper.AddStackedButtons(panel, new List<RibbonItemData>
            {
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_AnnotationOverlapDetection", "Overlap Detection", overlapV003),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_ParaManager", "ParaManager", paraManagerV003),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_WorksetsElementsBrowser", "Worksets & Elements", wsebV003),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_WorksetManager", "Workset Manager", worksetManagerV012),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_WorksetRenamer", "Workset Renamer", worksetRenamerV004),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_WorksetRenamerFx", "Workset Renamer (Excel)", worksetRenamerFx04),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_ScheduleExportImport", "Schedule Export/Import", scheduleExportImportV005),
            });
            RibbonLayoutHelper.WirePulldownButton(manageItems, "Pulldown_AnnotationOverlapDetection", overlapV003, overlapV004);
            RibbonLayoutHelper.WirePulldownButton(manageItems, "Pulldown_ParaManager", paraManagerV003, paraManagerV004);
            RibbonLayoutHelper.WirePulldownButton(manageItems, "Pulldown_WorksetsElementsBrowser", wsebV003, wsebV004);
            RibbonLayoutHelper.WirePulldownButton(manageItems, "Pulldown_WorksetManager", worksetManagerV012, worksetManagerV013);
            RibbonLayoutHelper.WirePulldownButton(manageItems, "Pulldown_WorksetRenamer", worksetRenamerV004, worksetRenamerV005);
            RibbonLayoutHelper.WirePulldownButton(manageItems, "Pulldown_WorksetRenamerFx", worksetRenamerFx04, worksetRenamerFx05);
            RibbonLayoutHelper.WirePulldownButton(manageItems, "Pulldown_ScheduleExportImport",
                scheduleExportImportV005, scheduleExportImportV006, scheduleExportImportV004, scheduleExportImportV003, scheduleExportImportV002);
        }
    }
}
