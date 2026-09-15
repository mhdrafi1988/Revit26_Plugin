using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
using System.Collections.Generic;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class RoofToolsRibbon
    {
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            // Combined launcher — its own small panel so it stays prominent.
            RibbonPanel combinedPanel = app.CreateRibbonPanel(tabName, "Combined Roof Tools");
            RibbonLayoutHelper.AddStackedButtons(combinedPanel, new List<PushButtonData>
            {
                new PushButtonData("Btn_CombinedRoofTools_V001", "Combined\nRoof Tools", assemblyPath, "Revit26_Plugin.CombinedRoofTools.V001.Commands.CombinedRoofToolsCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.CombinedTools_32.png"),
                    ToolTip = "Inner Loop Divider, Inner Loops And Perpendicular, Outer Curve Divider, " +
                        "Auto Slope By Drain, and Creaser Adv — combined in one window with one shared roof pick. " +
                        "Opens on Auto Slope By Drain; use Run All to run every tool in order with one click."
                },
            });

            // Slope
            RibbonPanel slopePanel = app.CreateRibbonPanel(tabName, "Roof Slope");
            RibbonLayoutHelper.AddStackedButtons(slopePanel, new List<PushButtonData>
            {
                new PushButtonData("Btn_AutoSlopeByPoint_028", "By Point", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint.V028.Commands.AutoSlopeCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.by_point.png"),
                    ToolTip = "Auto Slope By Point"
                },
                new PushButtonData("Btn_AutoSlopeByPoint_028_KdTree", "By Point (KD-Tree)", assemblyPath, "Revit26_Plugin.AutoSlopeByPointKdTree.VKD01.Commands.AutoSlopeCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.AutoSlopeByPoint_KdTree_32.png"),
                    ToolTip = "Auto Slope By Point (KD-Tree)"
                },
                new PushButtonData("Btn_AutoSlopeByPointRidge_001", "By Point (Ridge)", assemblyPath, "Revit26_Plugin.AutoSlopeByPointRidge.V001.Commands.AutoSlopeCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.AutoSlopeByPoint_Ridge_32.png"),
                    ToolTip = "Auto Slope By Point (Ridge) — V028 plus ridge handling: drains are grouped, ridge points are found on the basin boundaries " +
                              "between drain groups, and each ridge point is raised so water leaves it to every surrounding drain at the given slope."
                },
                new PushButtonData("Btn_AutoSlopeByDrain_V007", "By Drain", assemblyPath, "Revit26_Plugin.AutoSlopeByDrain.V007.Commands.AutoSlopeByDrain")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.by_drain.png"),
                    ToolTip = "Auto Slope By Drain"
                },
                new PushButtonData("Btn_AutoSlopeByDrain_V008", "By Drain (Multi)", assemblyPath, "Revit26_Plugin.MultiRoofSlopeByDrain.Commands.AutoSlopeByDrain")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.AutoSlopeByDrain_MultiRoof_32.png"),
                    ToolTip = "Auto Slope By Drain (Multi-Roof)"
                },
            });

            // Shape Points
            RibbonPanel shapePointsPanel = app.CreateRibbonPanel(tabName, "Shape Points");
            RibbonLayoutHelper.AddStackedButtons(shapePointsPanel, new List<PushButtonData>
            {
                new PushButtonData("Btn_InnerLoopDivider_V009", "Inner Loops", assemblyPath, "Revit26_Plugin.InnerLoopDivider.V009.Commands.InnerLoopDividerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.InnerLoopDivider_32.png"),
                    ToolTip = "Divide Inner Loops"
                },
                new PushButtonData("Btn_OuterCurveDivider_V004", "Outer Curve", assemblyPath, "Revit26_Plugin.OuterCurveDivider.V004.Commands.CurveDividerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.OuterCurveDivider_32.png"),
                    ToolTip = "Outer Curve Divider"
                },
                new PushButtonData("Btn_RoofDetailLineIntersect_V012", "Line Intersect", assemblyPath, "Revit26_Plugin.RoofDetailLineIntersect.V012.Commands.RoofDetailLineIntersectCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.DetailLineIntersect_32.png"),
                    ToolTip = "Roof Detail Line Intersect"
                },
                new PushButtonData("Btn_InnerLoopsAndPerpendicular_V005", "Loops + Perp.", assemblyPath, "Revit26_Plugin.InnerLoopsAndPerpendicular.V005.Commands.InnerLoopsAndPerpendicularCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.InnerLoopsPerpendicular_32.png"),
                    ToolTip = "Inner Loops And Perpendicular"
                },
                new PushButtonData("Btn_VertexReducer_V007", "Vertex Reducer", assemblyPath, "Revit26_Plugin.RoofEdgeVertexReducer.V007.Commands.RoofEdgeVertexReducerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.VertexReducer_32.png"),
                    ToolTip = "Roof Edge Vertex Reducer"
                },
                new PushButtonData("Btn_MultiplePoints_V001", "Multi Points", assemblyPath, "Revit26_Plugin.MultiplePoints.V001.Commands.MultiplePointsCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.MultiplePoints_32.png"),
                    ToolTip = "Multiple Points (1/2–1/4)"
                },
            });

            // Line & Point
            RibbonPanel linePointPanel = app.CreateRibbonPanel(tabName, "Line & Point");
            RibbonLayoutHelper.AddStackedButtons(linePointPanel, new List<PushButtonData>
            {
                new PushButtonData("Btn_RoofRidgeLines_V57", "By Points", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V057.Commands.RoofRidgeCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.RidgeLinesByPoints_32.png"),
                    ToolTip = "Roof Ridge Lines (By Points)"
                },
                new PushButtonData("Btn_RoofRidgeLines_V68", "Multi Shapes", assemblyPath, "Revit26_Plugin.RoofRidgeLines.V068.Commands.RoofRidgeCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.RidgeLinesMultiShape_32.png"),
                    ToolTip = "Roof Ridge Lines (Multiple Shapes)"
                },
            });

            // Slope Liner
            RibbonPanel slopeLinerPanel = app.CreateRibbonPanel(tabName, "Slope Liner");
            RibbonLayoutHelper.AddStackedButtons(slopeLinerPanel, new List<PushButtonData>
            {
                new PushButtonData("Btn_CreaserAdvCommand_V009_00", "Creaser Adv", assemblyPath, "Revit26_Plugin.CreaserAdv.V009.Commands.CreaserAdvCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.CreaserAdv_32.png")
                },
            });

            // Tag
            RibbonPanel tagPanel = app.CreateRibbonPanel(tabName, "Roof Tag");
            RibbonLayoutHelper.AddStackedButtons(tagPanel, new List<PushButtonData>
            {
                new PushButtonData("Btn_RoofTagCommand_V016", "Roof Tag", assemblyPath, "Revit26_Plugin.RoofTag.V016.RoofTagCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.RoofTag_32.png")
                },
            });

            // Create
            RibbonPanel createPanel = app.CreateRibbonPanel(tabName, "Roof Create");
            RibbonLayoutHelper.AddStackedButtons(createPanel, new List<PushButtonData>
            {
                new PushButtonData("Btn_RoofFromDetailLines.V007", "Roof From Detail Lines", assemblyPath, "Revit26_Plugin.RoofFromDetailLines.V007.Command")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.CreateRoofromLInes.png")
                },
            });

            // Compare
            RibbonPanel comparePanel = app.CreateRibbonPanel(tabName, "Compare");
            RibbonLayoutHelper.AddStackedButtons(comparePanel, new List<PushButtonData>
            {
                new PushButtonData("Btn_RoofPointComparison_V001", "Comparison", assemblyPath, "Revit26_Plugin.RoofPointComparison.V001.Commands.RoofComparisonCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.PointComparison_32.png"),
                    ToolTip = "Roof Point Comparison"
                },
                new PushButtonData("Btn_RoofPointElevationSync_V001", "Elevation Sync", assemblyPath, "Revit26_Plugin.RoofPointElevationSync.V002.Command")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.ElevationSync_32.png"),
                    ToolTip = "Roof Point Elevation Sync"
                },
            });
        }
    }
}
