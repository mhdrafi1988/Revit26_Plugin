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

            var paraManagerV001 = new PushButtonData("Btn_ParaManager_V001", "ParaManager V001", assemblyPath, "Revit26_Plugin.ParaManager.V001.ParaManagerCommand")
            {
                ToolTip = "ParaManager — bulk-assign shared parameters from a shared parameter file to categories, Instance or Type bound."
            };

            var paraManagerV002 = new PushButtonData("Btn_ParaManager_V002", "ParaManager V002", assemblyPath, "Revit26_Plugin.ParaManager.V002.ParaManagerCommand")
            {
                ToolTip = "ParaManager — bulk-assign shared parameters from a shared parameter file to categories, Instance or Type bound."
            };

            var paraManagerV003 = new PushButtonData("Btn_ParaManager_V003", "ParaManager V003", assemblyPath, "Revit26_Plugin.ParaManager.V003.ParaManagerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Manage.ParaManager_16.png"),
                ToolTip = "ParaManager — bulk-assign shared parameters from a shared parameter file to categories, Instance or Type bound, via a guided step-by-step flow."
            };

            var paraManagerPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_ParaManager", "ParaManager", paraManagerV003);

            var created = RibbonLayoutHelper.AddStackedButtons(panel, new List<RibbonItemData>
            {
                new PushButtonData("Btn_AnnotationOverlapDetection_V002", "Overlap Detection", assemblyPath, "Revit26_Plugin.AnnotationOverlapDetection.V002.Command")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Manage.AnnotationOverlapDetection_16.png"),
                    ToolTip = "Annotation Overlap Detection"
                },
                paraManagerPulldownData,
            });

            RibbonLayoutHelper.WirePulldownButton(created, "Pulldown_ParaManager", paraManagerV003, paraManagerV002, paraManagerV001);
        }
    }
}
