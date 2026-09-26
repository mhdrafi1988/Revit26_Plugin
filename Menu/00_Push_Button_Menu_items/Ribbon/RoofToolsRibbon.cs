using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
using System.Collections.Generic;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class RoofToolsRibbon
    {
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            // Combined launcher — the Ridge By Point variant that used to sit
            // here lives in the By Point pulldown below.
            RibbonPanel combinedPanel = app.CreateRibbonPanel(tabName, "Combined Roof Tools");
            RibbonLayoutHelper.AddStackedButtons(combinedPanel, new List<RibbonItemData>
            {
                new PushButtonData("Btn_CombinedRoofTools_V001", "Combined Roof Tools", assemblyPath, "Revit26_Plugin.CombinedRoofTools.V001.Commands.CombinedRoofToolsCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.CombinedTools_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Combined Roof Tools", "V001",
                        "Inner Loop Divider, Inner Loops And Perpendicular, Outer Curve Divider, " +
                        "Auto Slope By Drain, and Creaser Adv — combined in one window with one shared roof pick. " +
                        "Opens on Auto Slope By Drain; use Run All to run every tool in order with one click.")
                },
            });

            // Slope — By Point (3 coexisting implementations) and By Drain (2)
            // each collected under one pulldown button. V028 and V007 are the
            // base builds and keep their icons; the other variants are
            // each shown with an icon and text in its dropdown. Only versions
            // whose source still exists are listed here.
            RibbonPanel slopePanel = app.CreateRibbonPanel(tabName, "Roof Slope");

            var byPointV028 = new PushButtonData("Btn_AutoSlopeByPoint_028", "By Point V028", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint.V028.Commands.AutoSlopeCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.by_point_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Auto Slope By Point", "V028")
            };
            var byPointRidge = new PushButtonData("Btn_AutoSlopeByPointRidge_001", "By Point Ridge", assemblyPath, "Revit26_Plugin.AutoSlopeByPointRidge.V001.Commands.AutoSlopeCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.AutoSlopeByPoint_Ridge_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Auto Slope By Point (Ridge)", "V001",
                    "V028 plus ridge handling: drains are grouped, ridge points are found on the basin boundaries " +
                    "between drain groups, and each ridge point is raised so water leaves it to every surrounding drain at the given slope.")
            };
            var byPointMultiCopies = new PushButtonData("Btn_AutoSlopeByPoint_MultiCopies28", "By Point Multi Copies", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint.MultiCopies28.Commands.AutoSlopeCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.by_point_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Auto Slope By Point (Multi Copies)", "MultiCopies28 (V028 base)")
            };
            var byPointPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_AutoSlopeByPoint", "By Point", byPointV028);

            var byDrainV007 = new PushButtonData("Btn_AutoSlopeByDrain_V007", "By Drain V007", assemblyPath, "Revit26_Plugin.AutoSlopeByDrain.V007.Commands.AutoSlopeByDrain")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.by_drain_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Auto Slope By Drain", "V007")
            };
            var byDrainV010 = new PushButtonData("Btn_AutoSlopeByDrain_V010", "By Drain V010", assemblyPath, "Revit26_Plugin.MultiRoofSlopeByDrain.V010.Commands.AutoSlopeByDrain")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.AutoSlopeByDrain_MultiRoof_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Auto Slope By Drain (Multi-Roof)", "V010",
                    "Adds Start/End/Duration timing, a live progress bar with Cancel, " +
                    "Circle-group-only default expansion in the drain grid, and smallest-circle default selection.")
            };
            var byDrainPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_AutoSlopeByDrain", "By Drain", byDrainV007);

            var slopeItems = RibbonLayoutHelper.AddStackedButtons(slopePanel, new List<RibbonItemData> { byPointPulldownData, byDrainPulldownData });
            RibbonLayoutHelper.WirePulldownButton(slopeItems, "Pulldown_AutoSlopeByPoint", byPointV028, byPointRidge, byPointMultiCopies);
            RibbonLayoutHelper.WirePulldownButton(slopeItems, "Pulldown_AutoSlopeByDrain", byDrainV007, byDrainV010);

            // Shape Points
            RibbonPanel shapePointsPanel = app.CreateRibbonPanel(tabName, "Shape Points");
            RibbonLayoutHelper.AddStackedButtons(shapePointsPanel, new List<RibbonItemData>
            {
                new PushButtonData("Btn_InnerLoopDivider_V009", "Inner Loops", assemblyPath, "Revit26_Plugin.InnerLoopDivider.V009.Commands.InnerLoopDividerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.InnerLoopDivider_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Divide Inner Loops", "V009")
                },
                new PushButtonData("Btn_OuterCurveDivider_V004", "Outer Curve", assemblyPath, "Revit26_Plugin.OuterCurveDivider.V004.Commands.CurveDividerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.OuterCurveDivider_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Outer Curve Divider", "V004")
                },
                new PushButtonData("Btn_RoofDetailLineIntersect_V012", "Line Intersect", assemblyPath, "Revit26_Plugin.RoofDetailLineIntersect.V012.Commands.RoofDetailLineIntersectCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.DetailLineIntersect_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Roof Detail Line Intersect", "V012")
                },
                new PushButtonData("Btn_InnerLoopsAndPerpendicular_V005", "Loops + Perp.", assemblyPath, "Revit26_Plugin.InnerLoopsAndPerpendicular.V005.Commands.InnerLoopsAndPerpendicularCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.InnerLoopsPerpendicular_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Inner Loops And Perpendicular", "V005")
                },
                new PushButtonData("Btn_VertexReducer_V007", "Vertex Reducer", assemblyPath, "Revit26_Plugin.RoofEdgeVertexReducer.V007.Commands.RoofEdgeVertexReducerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.VertexReducer_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Roof Edge Vertex Reducer", "V007")
                },
                new PushButtonData("Btn_MultiplePoints_V001", "Multi Points", assemblyPath, "Revit26_Plugin.MultiplePoints.V001.Commands.MultiplePointsCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.MultiplePoints_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Multiple Points (1/2–1/4)", "V001")
                },
            });

            // Line & Point — By Points and Multi Shapes collected under one
            // pulldown per request; the higher-version build (V68) keeps the
            // icon; both versions show icon and text in the dropdown.
            RibbonPanel linePointPanel = app.CreateRibbonPanel(tabName, "Line & Point");

            var ridgeLinesMultiShape = new PushButtonData("Btn_RoofRidgeLines_V68", "Ridge By Openings V068", assemblyPath, "Revit26_Plugin.RoofRidgeLines.V068.Commands.RoofRidgeCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.RidgeLinesMultiShape_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Ridge By Openings", "V068")
            };
            var ridgeLinesByPoints = new PushButtonData("Btn_RoofRidgeLines_V57", "Ridge By Points V057", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V057.Commands.RoofRidgeCommand")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.RidgeLinesByPoints_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Ridge By Points", "V057")
            };
            var ridgeLinesPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_RoofRidgeLines", "Ridge Lines", ridgeLinesMultiShape);

            var linePointItems = RibbonLayoutHelper.AddStackedButtons(linePointPanel, new List<RibbonItemData> { ridgeLinesPulldownData });
            RibbonLayoutHelper.WirePulldownButton(linePointItems, "Pulldown_RoofRidgeLines", ridgeLinesMultiShape, ridgeLinesByPoints);

            // Slope Liner + Tag + Create — three single-tool panels merged into
            // one 3-item stack (Revit's stacked-item limit is exactly 3 per column).
            RibbonPanel roofToolsPanel = app.CreateRibbonPanel(tabName, "Roof Tools");
            RibbonLayoutHelper.AddStackedButtons(roofToolsPanel, new List<RibbonItemData>
            {
                new PushButtonData("Btn_CreaserAdvCommand_V010_00", "Creaser Adv", assemblyPath, "Revit26_Plugin.CreaserAdv.V010.Commands.CreaserAdvCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.CreaserAdv_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Creaser Adv", "V010")
                },
                new PushButtonData("Btn_RoofTagCommand_V016", "Roof Tag", assemblyPath, "Revit26_Plugin.RoofTag.V016.RoofTagCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.RoofTag_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Roof Tag", "V016")
                },
                new PushButtonData("Btn_RoofFromDetailLines.V007", "Roof From Detail Lines", assemblyPath, "Revit26_Plugin.RoofFromDetailLines.V007.Command")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.CreateRoofromLInes_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Roof From Detail Lines", "V007")
                },
            });

            // Type Manager
            RibbonPanel typeManagerPanel = app.CreateRibbonPanel(tabName, "Type Manager");
            RibbonLayoutHelper.AddStackedButtons(typeManagerPanel, new List<RibbonItemData>
            {
                new PushButtonData("Btn_RoofTypeManager_V001", "Roof Type Manager", assemblyPath, "Revit26_Plugin.RoofTypeCreator.V001.Commands.RoofTypeManagerCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.CreateRoofromLInes_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Roof Type Manager", "V001",
                        "Export roof type definitions to Excel (type mark, function, compound-structure layers) " +
                        "and re-import them into the model. Modeless — stays open while you work.")
                },
            });

            // Compare
            RibbonPanel comparePanel = app.CreateRibbonPanel(tabName, "Compare");

            var elevationSyncV003 = new PushButtonData("Btn_RoofPointElevationSync_V003", "Elevation Sync V003", assemblyPath, "Revit26_Plugin.RoofPointElevationSync.V003.Command")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.ElevationSync_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Roof Point Elevation Sync", "V003")
            };
            var elevationSyncV002 = new PushButtonData("Btn_RoofPointElevationSync_V002", "Elevation Sync V002", assemblyPath, "Revit26_Plugin.RoofPointElevationSync.V002.Command")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.ElevationSync_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Roof Point Elevation Sync", "V002")
            };
            var elevationSyncPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_RoofPointElevationSync", "Elevation Sync", elevationSyncV003);

            var compareItems = RibbonLayoutHelper.AddStackedButtons(comparePanel, new List<RibbonItemData>
            {
                new PushButtonData("Btn_RoofPointComparison_V001", "Comparison", assemblyPath, "Revit26_Plugin.RoofPointComparison.V001.Commands.RoofComparisonCommand")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.RoofTools.PointComparison_16.png"),
                    ToolTip = RibbonLayoutHelper.VersionTip("Roof Point Comparison", "V001")
                },
                elevationSyncPulldownData,
            });
            RibbonLayoutHelper.WirePulldownButton(compareItems, "Pulldown_RoofPointElevationSync", elevationSyncV003, elevationSyncV002);
        }
    }
}
