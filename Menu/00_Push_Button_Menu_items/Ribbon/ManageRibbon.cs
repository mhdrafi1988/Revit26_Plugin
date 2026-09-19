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

            const string paraManagerTip = "ParaManager — bulk-assign shared parameters from a shared parameter file to categories, Instance or Type bound.";

            RibbonLayoutHelper.AddStackedButtons(panel, new List<RibbonItemData>
            {
                new PushButtonData("Btn_AnnotationOverlapDetection_V002", "Overlap Detection", assemblyPath, "Revit26_Plugin.AnnotationOverlapDetection.V002.Command")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Manage.AnnotationOverlapDetection_16.png"),
                    ToolTip = "Annotation Overlap Detection"
                },
                new PushButtonData("Btn_ParaManager_V003", "ParaManager V003", assemblyPath, "Revit26_Plugin.ParaManager.V003.ParaManagerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Manage.ParaManager_16.png"),
                    ToolTip = paraManagerTip + " V003 uses a guided step-by-step flow."
                },
                new PushButtonData("Btn_ParaManager_V002", "ParaManager V002", assemblyPath, "Revit26_Plugin.ParaManager.V002.ParaManagerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Manage.ParaManager_16.png"),
                    ToolTip = paraManagerTip + " (V002)"
                },
                new PushButtonData("Btn_ParaManager_V001", "ParaManager V001", assemblyPath, "Revit26_Plugin.ParaManager.V001.ParaManagerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Manage.ParaManager_16.png"),
                    ToolTip = paraManagerTip + " (V001)"
                },
            });
        }
    }
}
