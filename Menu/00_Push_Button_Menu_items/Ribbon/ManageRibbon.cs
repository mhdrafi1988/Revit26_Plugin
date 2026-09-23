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
                new PushButtonData("Btn_WorksetsElementsBrowser_WSEB001", "Worksets & Elements", assemblyPath, "Revit26_Plugin.WorksetsElementsBrowser.WSEB001.Commands.WorksetsElementsBrowserCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Manage.WorksetsElementsBrowser_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Worksets & Elements Browser", "WSEB001",
                        "Browse worksets, categories and types in one checkbox tree; isolate or select checked elements in a chosen 3D view.")
                },
            });
        }
    }
}
