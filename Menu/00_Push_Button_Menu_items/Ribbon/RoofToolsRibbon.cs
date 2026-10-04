using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
using System.Collections.Generic;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class RoofToolsRibbon
    {
        // Every roof tool is a pulldown: the current version first, the
        // UI-standard rebuild (Revit_Plugin_UI_Standard.md layout) second.
        private const string Icons = "Revit26_Plugin.Resources.Icons.RoofTools.";

        private static PushButtonData Btn(string assemblyPath, string name, string text, string commandClass,
                                          string icon, string tool, string version, string detail = null)
        {
            return new PushButtonData(name, text, assemblyPath, commandClass)
            {
                Image = ImageUtils.Load(Icons + icon),
                ToolTip = RibbonLayoutHelper.VersionTip(tool, version, detail)
            };
        }

        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            // Utilities — three single-item panels merged into one 3-item stack
            // to save ribbon space: Combined Roof Tools, Ridge Lines, Type Manager.
            RibbonPanel utilitiesPanel = app.CreateRibbonPanel(tabName, "Utilities");

            // Slope — By Point (3 coexisting implementations) and By Drain (2)
            // each collected under one pulldown button. Each implementation's
            // UI-standard rebuild sits right after it in the dropdown.
            // Only versions whose source still exists are listed here.
            RibbonPanel slopePanel = app.CreateRibbonPanel(tabName, "Roof Slope");

            var byPointV028 = Btn(assemblyPath, "Btn_AutoSlopeByPoint_028", "By Point V028", "Revit26_Plugin.AutoSlopeByPoint.V028.Commands.AutoSlopeCommand",
                "by_point_16.png", "Auto Slope By Point", "V028");
            var byPointV029 = Btn(assemblyPath, "Btn_AutoSlopeByPoint_029", "By Point V029", "Revit26_Plugin.AutoSlopeByPoint.V029.Commands.AutoSlopeCommand",
                "by_point_16.png", "Auto Slope By Point", "V029", "V028 with the standard three-zone window layout.");
            var byPointRidge = Btn(assemblyPath, "Btn_AutoSlopeByPointRidge_001", "By Point Ridge", "Revit26_Plugin.AutoSlopeByPointRidge.V001.Commands.AutoSlopeCommand",
                "AutoSlopeByPoint_Ridge_16.png", "Auto Slope By Point (Ridge)", "V001",
                "V028 plus ridge handling: drains are grouped, ridge points are found on the basin boundaries " +
                "between drain groups, and each ridge point is raised so water leaves it to every surrounding drain at the given slope.");
            var byPointRidgeV002 = Btn(assemblyPath, "Btn_AutoSlopeByPointRidge_002", "By Point Ridge V002", "Revit26_Plugin.AutoSlopeByPointRidge.V002.Commands.AutoSlopeCommand",
                "AutoSlopeByPoint_Ridge_16.png", "Auto Slope By Point (Ridge)", "V002", "Ridge V001 with the standard three-zone window layout.");
            var byPointMultiCopies = Btn(assemblyPath, "Btn_AutoSlopeByPoint_MultiCopies28", "By Point Multi Copies", "Revit26_Plugin.AutoSlopeByPoint.MultiCopies28.Commands.AutoSlopeCommand",
                "by_point_16.png", "Auto Slope By Point (Multi Copies)", "MultiCopies28 (V028 base)");
            var byPointMultiCopies29 = Btn(assemblyPath, "Btn_AutoSlopeByPoint_MultiCopies29", "By Point Multi Copies 29", "Revit26_Plugin.AutoSlopeByPoint.MultiCopies29.Commands.AutoSlopeCommand",
                "by_point_16.png", "Auto Slope By Point (Multi Copies)", "MultiCopies29", "MultiCopies28 with the standard three-zone window layout.");
            var byPointPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_AutoSlopeByPoint", "By Point", byPointV028);

            var byDrainV007 = Btn(assemblyPath, "Btn_AutoSlopeByDrain_V007", "By Drain V007", "Revit26_Plugin.AutoSlopeByDrain.V007.Commands.AutoSlopeByDrain",
                "by_drain_16.png", "Auto Slope By Drain", "V007");
            var byDrainV011 = Btn(assemblyPath, "Btn_AutoSlopeByDrain_V011", "By Drain V011", "Revit26_Plugin.MultiRoofSlopeByDrain.V011.Commands.AutoSlopeByDrain",
                "AutoSlopeByDrain_MultiRoof_16.png", "Auto Slope By Drain (Multi-Roof)", "V011", "V010 with the standard three-zone window layout.");
            var byDrainV010 = Btn(assemblyPath, "Btn_AutoSlopeByDrain_V010", "By Drain V010", "Revit26_Plugin.MultiRoofSlopeByDrain.V010.Commands.AutoSlopeByDrain",
                "AutoSlopeByDrain_MultiRoof_16.png", "Auto Slope By Drain (Multi-Roof)", "V010",
                "Adds Start/End/Duration timing, a live progress bar with Cancel, " +
                "Circle-group-only default expansion in the drain grid, and smallest-circle default selection.");
            var byDrainPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_AutoSlopeByDrain", "By Drain", byDrainV007);

            var slopeItems = RibbonLayoutHelper.AddStackedButtons(slopePanel, new List<RibbonItemData> { byPointPulldownData, byDrainPulldownData });
            RibbonLayoutHelper.WirePulldownButton(slopeItems, "Pulldown_AutoSlopeByPoint", byPointV028, byPointV029, byPointRidge, byPointRidgeV002, byPointMultiCopies, byPointMultiCopies29);
            RibbonLayoutHelper.WirePulldownButton(slopeItems, "Pulldown_AutoSlopeByDrain", byDrainV007, byDrainV011, byDrainV010);

            // Shape Points — one pulldown per tool (current version, then its rebuild)
            RibbonPanel shapePointsPanel = app.CreateRibbonPanel(tabName, "Shape Points");

            var innerLoopsV010 = Btn(assemblyPath, "Btn_InnerLoopDivider_V010", "Inner Loops V010", "Revit26_Plugin.InnerLoopDivider.V010.Commands.InnerLoopDividerCommand",
                "InnerLoopDivider_16.png", "Divide Inner Loops", "V010", "V009 with the standard three-zone window layout.");
            var innerLoopsV011 = Btn(assemblyPath, "Btn_InnerLoopDivider_V011", "Inner Loops V011", "Revit26_Plugin.InnerLoopDivider.V011.Commands.InnerLoopDividerCommand",
                "InnerLoopDivider_16.png", "Divide Inner Loops", "V011", "V010 on the shared ToolWindowShell (log in body, capped grid).");
            var outerCurveV005 = Btn(assemblyPath, "Btn_OuterCurveDivider_V005", "Outer Curve V005", "Revit26_Plugin.OuterCurveDivider.V005.Commands.CurveDividerCommand",
                "OuterCurveDivider_16.png", "Outer Curve Divider", "V005", "V004 with the standard three-zone window layout.");
            var outerCurveV006 = Btn(assemblyPath, "Btn_OuterCurveDivider_V006", "Outer Curve V006", "Revit26_Plugin.OuterCurveDivider.V006.Commands.CurveDividerCommand",
                "OuterCurveDivider_16.png", "Outer Curve Divider", "V006", "V005 on the shared ToolWindowShell (log in body, capped grids).");
            var lineIntersectV013 = Btn(assemblyPath, "Btn_RoofDetailLineIntersect_V013", "Line Intersect V013", "Revit26_Plugin.RoofDetailLineIntersect.V013.Commands.RoofDetailLineIntersectCommand",
                "DetailLineIntersect_16.png", "Roof Detail Line Intersect", "V013", "V012 with the standard three-zone window layout.");
            var lineIntersectV014 = Btn(assemblyPath, "Btn_RoofDetailLineIntersect_V014", "Line Intersect V014", "Revit26_Plugin.RoofDetailLineIntersect.V014.Commands.RoofDetailLineIntersectCommand",
                "DetailLineIntersect_16.png", "Roof Detail Line Intersect", "V014", "V013 on the shared ToolWindowShell (log in body).");
            var loopsPerpV006 = Btn(assemblyPath, "Btn_InnerLoopsAndPerpendicular_V006", "Loops + Perp. V006", "Revit26_Plugin.InnerLoopsAndPerpendicular.V006.Commands.InnerLoopsAndPerpendicularCommand",
                "InnerLoopsPerpendicular_16.png", "Inner Loops And Perpendicular", "V006", "V005 with the standard three-zone window layout.");
            var loopsPerpV007 = Btn(assemblyPath, "Btn_InnerLoopsAndPerpendicular_V007", "Loops + Perp. V007", "Revit26_Plugin.InnerLoopsAndPerpendicular.V007.Commands.InnerLoopsAndPerpendicularCommand",
                "InnerLoopsPerpendicular_16.png", "Inner Loops And Perpendicular", "V007", "V006 on the shared ToolWindowShell (log in body, capped grids).");
            var vertexReducerV008 = Btn(assemblyPath, "Btn_VertexReducer_V008", "Vertex Reducer V008", "Revit26_Plugin.RoofEdgeVertexReducer.V008.Commands.RoofEdgeVertexReducerCommand",
                "VertexReducer_16.png", "Roof Edge Vertex Reducer", "V008", "V007 with the standard three-zone window layout.");
            var vertexReducerV009 = Btn(assemblyPath, "Btn_VertexReducer_V009", "Vertex Reducer V009", "Revit26_Plugin.RoofEdgeVertexReducer.V009.Commands.RoofEdgeVertexReducerCommand",
                "VertexReducer_16.png", "Roof Edge Vertex Reducer", "V009", "V008 on the shared ToolWindowShell (log in body, capped grid).");
            var multiPointsV002 = Btn(assemblyPath, "Btn_MultiplePoints_V002", "Multi Points V002", "Revit26_Plugin.MultiplePoints.V002.Commands.MultiplePointsCommand",
                "MultiplePoints_16.png", "Multiple Points (1/2–1/4)", "V002", "V001 with the standard three-zone window layout.");
            var multiPointsV003 = Btn(assemblyPath, "Btn_MultiplePoints_V003", "Multi Points V003", "Revit26_Plugin.MultiplePoints.V003.Commands.MultiplePointsCommand",
                "MultiplePoints_16.png", "Multiple Points (1/2–1/4)", "V003", "V002 on the shared ToolWindowShell (log in body, capped grid).");

            var shapePointsItems = RibbonLayoutHelper.AddStackedButtons(shapePointsPanel, new List<RibbonItemData>
            {
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_InnerLoopDivider", "Inner Loops", innerLoopsV010),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_OuterCurveDivider", "Outer Curve", outerCurveV005),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_RoofDetailLineIntersect", "Line Intersect", lineIntersectV013),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_InnerLoopsAndPerpendicular", "Loops + Perp.", loopsPerpV006),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_VertexReducer", "Vertex Reducer", vertexReducerV008),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_MultiplePoints", "Multi Points", multiPointsV002),
            });
            RibbonLayoutHelper.WirePulldownButton(shapePointsItems, "Pulldown_InnerLoopDivider", innerLoopsV010, innerLoopsV011);
            RibbonLayoutHelper.WirePulldownButton(shapePointsItems, "Pulldown_OuterCurveDivider", outerCurveV005, outerCurveV006);
            RibbonLayoutHelper.WirePulldownButton(shapePointsItems, "Pulldown_RoofDetailLineIntersect", lineIntersectV013, lineIntersectV014);
            RibbonLayoutHelper.WirePulldownButton(shapePointsItems, "Pulldown_InnerLoopsAndPerpendicular", loopsPerpV006, loopsPerpV007);
            RibbonLayoutHelper.WirePulldownButton(shapePointsItems, "Pulldown_VertexReducer", vertexReducerV008, vertexReducerV009);
            RibbonLayoutHelper.WirePulldownButton(shapePointsItems, "Pulldown_MultiplePoints", multiPointsV002, multiPointsV003);

            // Ridge Lines pulldown — merged into Utilities panel below.
            var ridgeLinesMultiShapeV069 = Btn(assemblyPath, "Btn_RoofRidgeLines_V69", "Ridge By Openings V069", "Revit26_Plugin.RoofRidgeLines.V069.Commands.RoofRidgeCommand",
                "RidgeLinesMultiShape_16.png", "Ridge By Openings", "V069", "V068 with the standard three-zone window layout.");
            var ridgeLinesMultiShapeV070 = Btn(assemblyPath, "Btn_RoofRidgeLines_V70", "Ridge By Openings V070", "Revit26_Plugin.RoofRidgeLines.V070.Commands.RoofRidgeCommand",
                "RidgeLinesMultiShape_16.png", "Ridge By Openings", "V070", "V069 on the shared ToolWindowShell (log in body, capped grid).");
            var ridgeLinesByPointsV058 = Btn(assemblyPath, "Btn_RoofRidgeLines_V58", "Ridge By Points V058", "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V058.Commands.RoofRidgeCommand",
                "RidgeLinesByPoints_16.png", "Ridge By Points", "V058", "V057 with the standard three-zone window layout.");
            var ridgeLinesByPointsV059 = Btn(assemblyPath, "Btn_RoofRidgeLines_V59", "Ridge By Points V059", "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V059.Commands.RoofRidgeCommand",
                "RidgeLinesByPoints_16.png", "Ridge By Points", "V059", "V058 on the shared ToolWindowShell (log in body, capped grid).");
            var ridgeLinesPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_RoofRidgeLines", "Ridge Lines", ridgeLinesMultiShapeV069);

            // Slope Liner + Tag + Create — three single-tool panels merged into
            // one 3-item stack (Revit's stacked-item limit is exactly 3 per column).
            RibbonPanel roofToolsPanel = app.CreateRibbonPanel(tabName, "Roof Tools");

            var creaserV011 = Btn(assemblyPath, "Btn_CreaserAdvCommand_V011", "Creaser Adv V011", "Revit26_Plugin.CreaserAdv.V011.Commands.CreaserAdvCommand",
                "CreaserAdv_16.png", "Creaser Adv", "V011", "V010 with the standard three-zone window layout.");
            var creaserV012 = Btn(assemblyPath, "Btn_CreaserAdvCommand_V012", "Creaser Adv V012", "Revit26_Plugin.CreaserAdv.V012.Commands.CreaserAdvCommand",
                "CreaserAdv_16.png", "Creaser Adv", "V012", "V011 on the shared ToolWindowShell (log in body).");
            var roofTagV017 = Btn(assemblyPath, "Btn_RoofTagCommand_V017", "Roof Tag V017", "Revit26_Plugin.RoofTag.V017.RoofTagCommand",
                "RoofTag_16.png", "Roof Tag", "V017", "V016 with the standard three-zone window layout.");
            var roofTagV018 = Btn(assemblyPath, "Btn_RoofTagCommand_V018", "Roof Tag V018", "Revit26_Plugin.RoofTag.V018.RoofTagCommand",
                "RoofTag_16.png", "Roof Tag", "V018", "V017 on the shared ToolWindowShell (log in body).");
            var roofFromLinesV007 = Btn(assemblyPath, "Btn_RoofFromDetailLines.V007", "Roof From Detail Lines V007", "Revit26_Plugin.RoofFromDetailLines.V007.Command",
                "CreateRoofromLInes_16.png", "Roof From Detail Lines", "V007");
            var roofFromLinesV008 = Btn(assemblyPath, "Btn_RoofFromDetailLines.V008", "Roof From Detail Lines V008", "Revit26_Plugin.RoofFromDetailLines.V008.Command",
                "CreateRoofromLInes_16.png", "Roof From Detail Lines", "V008", "V007 with the standard three-zone window layout.");

            var roofToolsItems = RibbonLayoutHelper.AddStackedButtons(roofToolsPanel, new List<RibbonItemData>
            {
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_CreaserAdv", "Creaser Adv", creaserV011),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_RoofTag", "Roof Tag", roofTagV017),
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_RoofFromDetailLines", "Roof From Detail Lines", roofFromLinesV007),
            });
            RibbonLayoutHelper.WirePulldownButton(roofToolsItems, "Pulldown_CreaserAdv", creaserV011, creaserV012);
            RibbonLayoutHelper.WirePulldownButton(roofToolsItems, "Pulldown_RoofTag", roofTagV017, roofTagV018);
            RibbonLayoutHelper.WirePulldownButton(roofToolsItems, "Pulldown_RoofFromDetailLines", roofFromLinesV007, roofFromLinesV008);

            // Utilities panel: Combined Roof Tools + Ridge Lines + Type Manager (3-item stack)
            const string combinedDetail =
                "Inner Loop Divider, Inner Loops And Perpendicular, Outer Curve Divider, " +
                "Auto Slope By Drain, and Creaser Adv — combined in one window with one shared roof pick. " +
                "Opens on Auto Slope By Drain; use Run All to run every tool in order with one click.";
            var combinedV001 = Btn(assemblyPath, "Btn_CombinedRoofTools_V001", "Combined Roof Tools V001", "Revit26_Plugin.CombinedRoofTools.V001.Commands.CombinedRoofToolsCommand",
                "CombinedTools_16.png", "Combined Roof Tools", "V001", combinedDetail);
            var combinedV002 = Btn(assemblyPath, "Btn_CombinedRoofTools_V002", "Combined Roof Tools V002", "Revit26_Plugin.CombinedRoofTools.V002.Commands.CombinedRoofToolsCommand",
                "CombinedTools_16.png", "Combined Roof Tools", "V002", "V001 with the standard three-zone window layout.");

            const string typeManagerDetail =
                "Export roof type definitions to Excel (type mark, function, compound-structure layers) " +
                "and re-import them into the model. Modeless — stays open while you work.";
            var typeManagerV001 = Btn(assemblyPath, "Btn_RoofTypeManager_V001", "Roof Type Manager V001", "Revit26_Plugin.RoofTypeCreator.V001.Commands.RoofTypeManagerCommand",
                "CreateRoofromLInes_16.png", "Roof Type Manager", "V001", typeManagerDetail);
            var typeManagerV002 = Btn(assemblyPath, "Btn_RoofTypeManager_V002", "Roof Type Manager V002", "Revit26_Plugin.RoofTypeCreator.V002.Commands.RoofTypeManagerCommand",
                "CreateRoofromLInes_16.png", "Roof Type Manager", "V002", "V001 with the standard three-zone window layout.");

            var utilitiesItems = RibbonLayoutHelper.AddStackedButtons(utilitiesPanel, new List<RibbonItemData>
            {
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_CombinedRoofTools", "Combined Roof Tools", combinedV001),
                ridgeLinesPulldownData,
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_RoofTypeManager", "Roof Type Manager", typeManagerV001),
            });
            RibbonLayoutHelper.WirePulldownButton(utilitiesItems, "Pulldown_CombinedRoofTools", combinedV001, combinedV002);
            RibbonLayoutHelper.WirePulldownButton(utilitiesItems, "Pulldown_RoofRidgeLines", ridgeLinesMultiShapeV069, ridgeLinesMultiShapeV070, ridgeLinesByPointsV058, ridgeLinesByPointsV059);
            RibbonLayoutHelper.WirePulldownButton(utilitiesItems, "Pulldown_RoofTypeManager", typeManagerV001, typeManagerV002);

            // Compare
            RibbonPanel comparePanel = app.CreateRibbonPanel(tabName, "Compare");

            var comparisonV002 = Btn(assemblyPath, "Btn_RoofPointComparison_V002", "Comparison V002", "Revit26_Plugin.RoofPointComparison.V002.Commands.RoofComparisonCommand",
                "PointComparison_16.png", "Roof Point Comparison", "V002", "V001 with the standard three-zone window layout.");
            var comparisonV003 = Btn(assemblyPath, "Btn_RoofPointComparison_V003", "Comparison V003", "Revit26_Plugin.RoofPointComparison.V003.Commands.RoofComparisonCommand",
                "PointComparison_16.png", "Roof Point Comparison", "V003", "V002 on the shared ToolWindowShell (log in body).");

            var elevationSyncV003 = Btn(assemblyPath, "Btn_RoofPointElevationSync_V003", "Elevation Sync V003", "Revit26_Plugin.RoofPointElevationSync.V003.Command",
                "ElevationSync_16.png", "Roof Point Elevation Sync", "V003");
            var elevationSyncV004 = Btn(assemblyPath, "Btn_RoofPointElevationSync_V004", "Elevation Sync V004", "Revit26_Plugin.RoofPointElevationSync.V004.Command",
                "ElevationSync_16.png", "Roof Point Elevation Sync", "V004", "V003 with the standard three-zone window layout.");
            var elevationSyncV005 = Btn(assemblyPath, "Btn_RoofPointElevationSync_V005", "Elevation Sync V005", "Revit26_Plugin.RoofPointElevationSync.V005.Command",
                "ElevationSync_16.png", "Roof Point Elevation Sync", "V005", "V004 on the shared ToolWindowShell (log in body, capped grid).");
            var elevationSyncPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_RoofPointElevationSync", "Elevation Sync", elevationSyncV003);

            var compareItems = RibbonLayoutHelper.AddStackedButtons(comparePanel, new List<RibbonItemData>
            {
                RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_RoofPointComparison", "Comparison", comparisonV002),
                elevationSyncPulldownData,
            });
            RibbonLayoutHelper.WirePulldownButton(compareItems, "Pulldown_RoofPointComparison", comparisonV002, comparisonV003);
            RibbonLayoutHelper.WirePulldownButton(compareItems, "Pulldown_RoofPointElevationSync", elevationSyncV003, elevationSyncV004, elevationSyncV005);
        }
    }
}
