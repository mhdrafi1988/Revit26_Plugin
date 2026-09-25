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

            RibbonLayoutHelper.AddStackedButtons(panel, new List<RibbonItemData>
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
                new PushButtonData("Btn_WorksetManager_11", "Worksets From Links", assemblyPath, "Revit26_Plugin.WSFL.V011.Commands.CreateWorksetsFromLinkedFiles")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.SetupTools.WorksetsFromLinks_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Create Worksets From Linked Files", "V011")
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
            });
        }
    }
}
