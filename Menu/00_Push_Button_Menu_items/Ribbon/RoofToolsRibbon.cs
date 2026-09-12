using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
using System;
using System.Reflection;
using System.Windows.Markup;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class RoofToolsRibbon
    {

        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = app.CreateRibbonPanel(tabName, "Roof Tools");

            PushButton combinedBtn = panel.AddItem(new PushButtonData(
                "Btn_CombinedRoofTools_V001",
                "Combined\nRoof Tools",
                assemblyPath,
                "Revit26_Plugin.CombinedRoofTools.V001.Commands.CombinedRoofToolsCommand")) as PushButton;
            combinedBtn.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.CombinedTools_32.png");
            combinedBtn.ToolTip = "Inner Loop Divider, Inner Loops And Perpendicular, Outer Curve Divider, " +
                "Auto Slope By Drain, and Creaser Adv — combined in one window with one shared roof pick. " +
                "Opens on Auto Slope By Drain; use Run All to run every tool in order with one click.";

            PulldownButton SlopeMenu = panel.AddItem(new PulldownButtonData("RoofSlopeMenu", "Auto Slope")) as PulldownButton;
            SlopeMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.Autoslope32.png");

            //Slope By Point
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint_028", "Auto Slope By Point — V028", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint.V028.Commands.AutoSlopeCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.by_point.png")
            });
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint_028_KdTree", "Auto Slope By Point — V028 (KD-Tree test)", assemblyPath, "Revit26_Plugin.AutoSlopeByPointKdTree.VKD01.Commands.AutoSlopeCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.AutoSlopeByPoint_KdTree_32.png")
            });
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint_MultiCopies28", "Auto Slope By Point — MultiCopies28 (Multi-Slope Variants)", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint.MultiCopies28.Commands.AutoSlopeCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.by_point.png"),
                ToolTip = "Generates up to 4 roof copies (drain point XY preserved) at different slope percentages, each assigned to its own workset."
            });

            //Slope By Drain
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByDrain_V007", "Auto Slope By Drain — V007", assemblyPath, "Revit26_Plugin.AutoSlopeByDrain.V007.Commands.AutoSlopeByDrain")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.by_drain.png")
            });
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByDrain_V008", "Auto Slope By Drain — V008 (Multi-Roof)", assemblyPath, "Revit26_Plugin.MultiRoofSlopeByDrain.Commands.AutoSlopeByDrain")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.AutoSlopeByDrain_MultiRoof_32.png")
            });

            //Create Shape Point tools
            PulldownButton ShapepointMenu = panel.AddItem(new PulldownButtonData("ShapepointMenu", "Shape Points")) as PulldownButton;
            ShapepointMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.Shapepoints32.png");
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_InnerLoopDivider_V009", "Divide Inner Loops — V009", assemblyPath, "Revit26_Plugin.InnerLoopDivider.V009.Commands.InnerLoopDividerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.InnerLoopDivider_32.png")
            });
            //ShapepointMenu.AddPushButton(new PushButtonData("Btn_RoofLoopAnalyzerPDC_V005", "Roof Loop Analyzer PDC — V005", assemblyPath, "Revit26_Plugin.RoofLoopAnalyzerPDC.V005.Commands.RoofLoopAnalyzerPDCCommand"));
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_OuterCurveDivider_V004", "Outer Curve Divider — V004", assemblyPath, "Revit26_Plugin.OuterCurveDivider.V004.Commands.CurveDividerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.OuterCurveDivider_32.png")
            });
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_RoofDetailLineIntersect_V012", "Roof Detail Line Intersect — V012", assemblyPath, "Revit26_Plugin.RoofDetailLineIntersect.V012.Commands.RoofDetailLineIntersectCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.DetailLineIntersect_32.png")
            });
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_InnerLoopsAndPerpendicular_V005", "Inner Loops And Perpendicular — V005", assemblyPath, "Revit26_Plugin.InnerLoopsAndPerpendicular.V005.Commands.InnerLoopsAndPerpendicularCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.InnerLoopsPerpendicular_32.png")
            });
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_VertexReducer_V007", "Roof Edge Vertex Reducer — V007", assemblyPath, "Revit26_Plugin.RoofEdgeVertexReducer.V007.Commands.RoofEdgeVertexReducerCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.VertexReducer_32.png")
            });
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_MultiplePoints_V001", "Multiple Points(1/2-1/4) — V001", assemblyPath, "Revit26_Plugin.MultiplePoints.V001.Commands.MultiplePointsCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.MultiplePoints_32.png")
            });

            PulldownButton LineAndPoint = panel.AddItem(new PulldownButtonData("LineAndPointMenu", "Line-Point")) as PulldownButton;
            LineAndPoint.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.LinePoint.png");

            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V57", "Roof Ridge Lines (By Points) — V057", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V057.Commands.RoofRidgeCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.RidgeLinesByPoints_32.png")
            });
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V68", "Roof Ridge Lines (Multiple Shapes) — V068", assemblyPath, "Revit26_Plugin.RoofRidgeLines.V068.Commands.RoofRidgeCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.RidgeLinesMultiShape_32.png")
            });

            //Slope Liner Menu
            PulldownButton SlopeLinerMenu = panel.AddItem(new PulldownButtonData("SlopeLiner", "Slope Liner")) as PulldownButton;
            SlopeLinerMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.Divider.png");
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V009_00", "Creaser Adv — V009", assemblyPath, "Revit26_Plugin.CreaserAdv.V009.Commands.CreaserAdvCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.CreaserAdv_32.png")
            });

            PulldownButton tagMenu = panel.AddItem(new PulldownButtonData("RoofTagMenu", "Tag")) as PulldownButton;
            tagMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.Addtag32.png");
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommand_V016", "Roof Tag — V016", assemblyPath, "Revit26_Plugin.RoofTag.V016.RoofTagCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.RoofTag_32.png")
            });

            PulldownButton CreateMenu = panel.AddItem(new PulldownButtonData("RoofCreateMenu", "Create")) as PulldownButton;
            CreateMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.SlopeLiner.png");
            CreateMenu.AddPushButton(new PushButtonData("Btn_RoofFromDetailLines.V007", "Roof From Detail Lines — V007", assemblyPath, "Revit26_Plugin.RoofFromDetailLines.V007.Command")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.CreateRoofromLInes.png")
            });

            PulldownButton CompareMenu = panel.AddItem(new PulldownButtonData("RoofCompareMenu", "Compare")) as PulldownButton;
            CompareMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.Compare_32.png");
            CompareMenu.AddPushButton(new PushButtonData("Btn_RoofPointComparison_V001", "Roof Point Comparison — V001", assemblyPath, "Revit26_Plugin.RoofPointComparison.V001.Commands.RoofComparisonCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.PointComparison_32.png")
            });
            CompareMenu.AddPushButton(new PushButtonData("Btn_RoofPointElevationSync_V001", "Roof Point Elevation Sync — V001", assemblyPath, "Revit26_Plugin.RoofPointElevationSync.V002.Command")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.ElevationSync_32.png")
            });
        }
    }
}