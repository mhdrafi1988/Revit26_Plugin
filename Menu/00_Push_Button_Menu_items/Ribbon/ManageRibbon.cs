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

            var paraManagerV004 = Btn(assemblyPath, "Btn_ParaManager_V004", "ParaManager V004", "Revit26_Plugin.ParaManager.V004.ParaManagerCommand",
                "Manage.ParaManager_16.png", "ParaManager", "V004", "V003" + Rebuilt);
            var paraManagerV005 = Btn(assemblyPath, "Btn_ParaManager_V005", "ParaManager V005", "Revit26_Plugin.ParaManager.V005.ParaManagerCommand",
                "Manage.ParaManager_16.png", "ParaManager", "V005", "V004 on the shared ToolWindowShell (log in body, capped queue grid).");

            var wsebV003 = Btn(assemblyPath, "Btn_WorksetsElementsBrowser_WSEB003", "Worksets & Elements WSEB003", "Revit26_Plugin.WorksetsElementsBrowser.WSEB003.Commands.WorksetsElementsBrowserCommand",
                "Manage.WorksetsElementsBrowser_16.png", "Worksets & Elements Browser", "WSEB003", "WSEB002" + Rebuilt);
            var wsebV004 = Btn(assemblyPath, "Btn_WorksetsElementsBrowser_WSEB004", "Worksets & Elements WSEB004", "Revit26_Plugin.WorksetsElementsBrowser.WSEB004.Commands.WorksetsElementsBrowserCommand",
                "Manage.WorksetsElementsBrowser_16.png", "Worksets & Elements Browser", "WSEB004", "WSEB003 on the shared ToolWindowShell (capped tree).");

            var worksetManagerV013 = Btn(assemblyPath, "Btn_WorksetManager_V013", "Workset Manager V013", "Revit26_Plugin.WorksetManager.V013.Commands.WorksetManagerCommand",
                "SetupTools.WorksetManager_16.png", "Workset Manager", "V013", "V012" + Rebuilt);
            var worksetManagerV014 = Btn(assemblyPath, "Btn_WorksetManager_V014", "Workset Manager V014", "Revit26_Plugin.WorksetManager.V014.Commands.WorksetManagerCommand",
                "SetupTools.WorksetManager_16.png", "Workset Manager", "V014", "V013 on the shared ToolWindowShell (log in body, capped grids).");

            var worksetRenamerV004 = Btn(assemblyPath, "Btn_WorksetRenamer_V004", "Workset Renamer V004", "Revit26_Plugin.WorksetRenamer.V004.Command",
                "SetupTools.WorksetRename_16.png", "Workset Renamer", "V004", "V003" + Rebuilt);
            var worksetRenamerV005 = Btn(assemblyPath, "Btn_WorksetRenamer_V005", "Workset Renamer V005", "Revit26_Plugin.WorksetRenamer.V005.Command",
                "SetupTools.WorksetRename_16.png", "Workset Renamer", "V005", "V004 on the shared ToolWindowShell (capped grid).");

            var deleteWorksetV001 = Btn(assemblyPath, "Btn_DeleteWorkset_V001", "Delete Workset V001",
                "Revit26_Plugin.DeleteWorkset.V001.Commands.DeleteWorksetCommand",
                "SetupTools.WorksetManager_16.png", "Delete Workset", "V001",
                "Deletes one or more worksets and migrates their elements to a chosen target workset.");

            var deleteLineStylesV001 = Btn(assemblyPath, "Btn_DeleteLineStyles_V001", "Delete Line Styles V001",
                "Revit26_Plugin.DeleteLineStyles.V001.Commands.DeleteLineStylesCommand",
                "Manage.DeleteLineStyles_16.png", "Delete Line Styles", "V001");
            deleteLineStylesV001.ToolTip = Revit26_Plugin.Shared.Services.ToolCatalog.DeleteLineStyles.Tip(
                "Deletes unused custom line styles, or all custom line styles (their lines move to a replacement style).");

            var worksetRenamerFx04 = Btn(assemblyPath, "Btn_WorksetRenamer_FX04", "Workset Renamer (Excel) FX04", "Revit26_Plugin.WorksetRenamer.FX04.Command",
                "SetupTools.WorksetRename_16.png", "Workset Renamer (From Excel)", "FX04", "FX03" + Rebuilt);
            var worksetRenamerFx05 = Btn(assemblyPath, "Btn_WorksetRenamer_FX05", "Workset Renamer (Excel) FX05", "Revit26_Plugin.WorksetRenamer.FX05.Command",
                "SetupTools.WorksetRename_16.png", "Workset Renamer (Excel)", "FX05", "FX04 on the shared ToolWindowShell (capped group grids, shared group colours).");

            // Schedule Export/Import — every working version in one dropdown: the current
            // version (V005) first, its rebuild (V006) second, the ToolWindowShell version
            // (V007) third, then the older versions. V001 is left out: its export can never
            // complete (log-scroll bug fixed in V002); V002 was dropped when V007 was added.
            var scheduleExportImportV006 = Btn(assemblyPath, "Btn_ScheduleExportImport_V006", "Schedule Export/Import V006", "Revit26_Plugin.ScheduleExportImport.V006.Commands.ScheduleExportImportCommand",
                "Manage.ScheduleExportImport_16.png", "Schedule Export / Import", "V006", "V005" + Rebuilt);
            var scheduleExportImportV007 = Btn(assemblyPath, "Btn_ScheduleExportImport_V007", "Schedule Export/Import V007", "Revit26_Plugin.ScheduleExportImport.V007.Commands.ScheduleExportImportCommand",
                "Manage.ScheduleExportImport_16.png", "Schedule Export / Import", "V007", "V006 on the shared ToolWindowShell (log in body, capped grids).");
            var scheduleExportImportV005 = Btn(assemblyPath, "Btn_ScheduleExportImport_V005", "Schedule Export/Import V005", "Revit26_Plugin.ScheduleExportImport.V005.Commands.ScheduleExportImportCommand",
                "Manage.ScheduleExportImport_16.png", "Schedule Export / Import", "V005",
                "Flags values changed in Revit since export, checks element ownership, compares numbers by value, Excel dropdowns, type-parameter edits, hidden fields, remembers the folder.");
            var scheduleExportImportV004 = Btn(assemblyPath, "Btn_ScheduleExportImport_V004", "Schedule Export/Import V004", "Revit26_Plugin.ScheduleExportImport.V004.Commands.ScheduleExportImportCommand",
                "Manage.ScheduleExportImport_16.png", "Schedule Export / Import", "V004",
                "Export / Import buttons in a row under the metric cards; schedule list full width. Everything from V003.");
            var scheduleExportImportV003 = Btn(assemblyPath, "Btn_ScheduleExportImport_V003", "Schedule Export/Import V003", "Revit26_Plugin.ScheduleExportImport.V003.Commands.ScheduleExportImportCommand",
                "Manage.ScheduleExportImport_16.png", "Schedule Export / Import", "V003",
                "Errors always shown, import picks the schedule from the file, explains 'nothing to import', Base Level by name.");

            var manageItems = RibbonLayoutHelper.AddStackedButtons(panel, new List<RibbonItemData>
            {
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_AnnotationOverlapDetection", "Overlap Detection", overlapV003),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_ParaManager", "ParaManager", paraManagerV004),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_WorksetsElementsBrowser", "Worksets & Elements", wsebV003),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_WorksetManager", "Workset Manager", worksetManagerV013),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_WorksetRenamer", "Workset Renamer", worksetRenamerV004),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_WorksetRenamerFx", "Workset Renamer (Excel)", worksetRenamerFx04),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_DeleteWorkset", "Delete Workset", deleteWorksetV001),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_DeleteLineStyles", "Delete Line Styles", deleteLineStylesV001),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_ScheduleExportImport", "Schedule Export/Import", scheduleExportImportV005),
            });
            RibbonLayoutHelper.WirePulldownButton(manageItems, "Pulldown_AnnotationOverlapDetection", overlapV003, overlapV004);
            RibbonLayoutHelper.WirePulldownButton(manageItems, "Pulldown_ParaManager", paraManagerV004, paraManagerV005);
            RibbonLayoutHelper.WirePulldownButton(manageItems, "Pulldown_WorksetsElementsBrowser", wsebV003, wsebV004);
            RibbonLayoutHelper.WirePulldownButton(manageItems, "Pulldown_WorksetManager", worksetManagerV013, worksetManagerV014);
            RibbonLayoutHelper.WirePulldownButton(manageItems, "Pulldown_WorksetRenamer", worksetRenamerV004, worksetRenamerV005);
            RibbonLayoutHelper.WirePulldownButton(manageItems, "Pulldown_WorksetRenamerFx", worksetRenamerFx04, worksetRenamerFx05);
            RibbonLayoutHelper.WirePulldownButton(manageItems, "Pulldown_DeleteWorkset", deleteWorksetV001);
            RibbonLayoutHelper.WirePulldownButton(manageItems, "Pulldown_DeleteLineStyles", deleteLineStylesV001);
            RibbonLayoutHelper.WirePulldownButton(manageItems, "Pulldown_ScheduleExportImport",
                scheduleExportImportV005, scheduleExportImportV006, scheduleExportImportV007, scheduleExportImportV004, scheduleExportImportV003);
        }
    }
}
