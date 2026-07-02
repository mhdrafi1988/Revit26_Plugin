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

            PulldownButton SlopeMenu = panel.AddItem(new PulldownButtonData("RoofSlopeMenu", "Auto SLope")) as PulldownButton;
            SlopeMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Autoslope32.png");

            //Slope BY Point

            //SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint_05", "AutoSlopeByPoint_05", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint_05.Commands.AutoSlopeCommand"));
            //SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint_06", "AutoSlopeByPoint_06_New Dijkstra", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint.V06.Commands.AutoSlopeCommand"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint_009", "AutoSlopeByPoint_009_old Dijkstra", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint.V009.Commands.AutoSlopeCommand"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint_010", "AutoSlopeByPoint_010_New Dijkstra", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint.V010.Commands.AutoSlopeCommand"));

            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint", "AutoSlopeByPoint_00_00(Classic)", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint.Commands.AutoSlopeCommand"));
            //SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint_04", "AutoSlope(ByPoint)00_04_Excel(Classic)", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint_04.Commands.AutoSlopeCommand"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPointTwoSlopes_01_00", "AutoSlopeByPointTwoSlopes_01_00_Excel(WIP)", assemblyPath, "AutoSlopeByPointTwoSlopes_01_00.Commands.AutoSlopeCommand"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_RoofEdgeVertexReducer", "RoofEdgeVertexReducer", assemblyPath, "Revit26_Plugin.RoofEdgeVertexReducer.V001.Commands.RoofEdgeVertexReducerCommand"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_RoofEdgeVertexReducer V02", "RoofEdgeVertexReducer V02", assemblyPath, "Revit26_Plugin.RoofEdgeVertexReducer.V002.Commands.RoofEdgeVertexReducerCommand"));
            //SlopeMenu.AddPushButton(new PushButtonData("Btn_DijkstraPath2_2026", "DijkstraPath2_2026(Point)", assemblyPath, "Revit26_Plugin.Commands.DijkstraPath2_2026"));
            //SlopeMenu.AddPushButton(new PushButtonData("Btn_RoofSloperClassic1_v2", "RoofSloperClassic1_V2_CSV(Point)", assemblyPath, "Revit26_Plugin.Commands.RoofSloperClassic1_v2"));

            //Slope BY Drain 

            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSloperDrain_Asd_19", "AutoSloperDrain_Asd_19_CSV(Drain)", assemblyPath, "Revit26_Plugin.Asd_19.Commands.AutoSloperDrain_04"));

            //Create Shape Point Shape Point Shape Point Shape Point

            PulldownButton ShapepointMenu = panel.AddItem(new PulldownButtonData("ShapepointMenu", "Shape Points")) as PulldownButton;
            ShapepointMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Shapepoints32.png");

            //ShapepointMenu.AddPushButton(new PushButtonData("Btn_ADivideInnerLoops_V001", "Divide Inner Loops(Basic)", assemblyPath, "Revit22_Plugin.PDCV1.Commands.RoofLoopAnalyzerCommand_01"));//Working
            //ShapepointMenu.AddPushButton(new PushButtonData("Btn_ADivideInnerLoops_V004", "Divide Inner Loops   V004", assemblyPath, "Revit26_Plugin.DivideInnerLoops.V004.RoofLoopAnalyzerCommand"));//Working
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_ADivideInnerLoops_V006", "Divide Inner Loops   V006", assemblyPath, "Revit26_Plugin.DivideInnerLoops.V006.RoofLoopAnalyzerCommand"));//Working

            //ShapepointMenu.AddPushButton(new PushButtonData("Btn_ADivideInnerLoops_V002", "Divide Inner Loops_PDCV2 ", assemblyPath, "Revit26_Plugin.PDCV2.Commands.RoofLoopAnalyzerCommand"));//Working 
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_AddPointOnIntersectionsCommand 05", "Outer CurveDivider.V001", assemblyPath, "Revit26_Plugin.OuterCurveDivider.V001.Commands.CurveDividerCommand"));//Working
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_RoofDetailLineIntersect 04", "Roof Detail Line Intersect V0004 #", assemblyPath, "Revit26_Plugin.RoofDetailLineIntersect.V004.RoofDetailLineIntersectCommand"));//Working
            //ShapepointMenu.AddPushButton(new PushButtonData("Btn_RoofDetailLineIntersect 05", "Roof Detail Line Intersect V0005 #", assemblyPath, "Revit26_Plugin.RoofDetailLineIntersect.V005.RoofDetailLineIntersectCommand"));//Working
            //ShapepointMenu.AddPushButton(new PushButtonData("Btn_RoofDetailLineIntersect 06", "Roof Detail Line Intersect V0006 #", assemblyPath, "Revit26_Plugin.RoofDetailLineIntersect.V006.RoofDetailLineIntersectCommand"));//Working
            //ShapepointMenu.AddPushButton(new PushButtonData("Btn_RoofDetailLineIntersect 07", "Roof Detail Line Intersect V0007 #", assemblyPath, "Revit26_Plugin.RoofDetailLineIntersect.V007.RoofDetailLineIntersectCommand"));//Working
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_RoofDetailLineIntersect 08", "Roof Detail Line Intersect V0008 #", assemblyPath, "Revit26_Plugin.RoofDetailLineIntersect.V008.RoofDetailLineIntersectCommand"));//Working

            ShapepointMenu.AddPushButton(new PushButtonData("Btn_PonitOnCurvesInnerandOuter 01", "Ponits On Curves Inner & Outer V02", assemblyPath, "Revit26_Plugin.PonitOnCurvesInnerandOuter.V01.Commands.RoofLoopAnalyzerCommand"));//Working
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_DivideInnerLoopsAndPerpendicular_V002", "DivideInnerLoopsAndPerpendicular V002##", assemblyPath, "Revit26_Plugin.DivideInnerLoopsAndPerpendicular.V002.PerpendicularPointCommand"));//Working




            PulldownButton LineAndPoint = panel.AddItem(new PulldownButtonData("LineAndPointMenu", "Line & PointMenu")) as PulldownButton;
            LineAndPoint.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Linematch32.png");
                        
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V56", "Auto Ridger(Multiple Shapes)56(By Point)", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V56.Commands.RoofRidgeCommand"));//Working
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V60", "Auto Ridger(Multiple Shapes)60(By Shape)", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V60.Commands.RoofRidgeCommand"));//Working
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V62", "Auto Ridger(Multiple Shapes)62(By Shape)", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V62.Commands.RoofRidgeCommand"));//Working
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V64", "Auto Ridger(Multiple Shapes)64(By Shape)", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V64.Commands.RoofRidgeCommand"));//Working
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V66", "Auto Ridger(Multiple Shapes)66(By Shape)", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V66.Commands.RoofRidgeCommand"));//Working

            //Slope Liner Menu
            PulldownButton SlopeLinerMenu = panel.AddItem(new PulldownButtonData("SlopeLiner", "SlopeLiner")) as PulldownButton;
            SlopeLinerMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Addlines_32.png");

            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V002_01", "CreaserAdvCommand V002_01 # Working", assemblyPath, "Revit26_Plugin.CreaserAdv_V002_01.Commands.CreaserAdvCommand"));
            
            
            PulldownButton tagMenu = panel.AddItem(new PulldownButtonData("RoofTagMenu", "Tag")) as PulldownButton;
            tagMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Addtag32.png");
            //tagMenu.LargeImage = IconManager.ToBitmapSource(Properties.Resources.addtag32);
                        
            //tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommandV3", "RoofTagCommandV3", assemblyPath, "Revit22_Plugin.RoofTag_V90.RoofTagCommandV3"));
            //tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommand_V5)", "RoofTagCommand V5", assemblyPath, "Revit26_Plugin.RoofTag_V73.Commands.RoofTagCommand"));
            //tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommand_V006)", "RoofTagCommand V006", assemblyPath, "Revit26_Plugin.RoofTag_V006.RoofTagCommand"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommand_V007A)", "RoofTagCommand V007 A Face Ref", assemblyPath, "Revit26_Plugin.RoofTag_V007_A.RoofTagCommand"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommand_V008)", "RoofTagCommand V008", assemblyPath, "Revit26_Plugin.RoofTag_V008.RoofTagCommand"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommand_V011)", "RoofTagCommand V011", assemblyPath, "Revit26_Plugin.RoofTag_V011.RoofTagCommand"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommand_V012)", "RoofTagCommand V012", assemblyPath, "Revit26_Plugin.RoofTag_V012.RoofTagCommand"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommand_V013)", "RoofTagCommand V013", assemblyPath, "Revit26_Plugin.RoofTag_V013.RoofTagCommand"));






            PulldownButton Profiler = panel.AddItem(new PulldownButtonData("ProfilerTagMenu", "Profiler")) as PulldownButton;
            Profiler.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Addtag32.png");
                        
            //Profiler.AddPushButton(new PushButtonData("Btn_RoofFromFloor_V02", "Roof From Floor V02", assemblyPath, "Revit26_Plugin.RoofFromFloor.V02.Commands.LaunchRoofFromFloorCommand"));
            //Profiler.AddPushButton(new PushButtonData("Btn_RoofFromFloor_Base", "Roof From Floor Base", assemblyPath, "Revit26_Plugin.RoofFromFloor.Commands.LaunchRoofFromFloorCommand"));
            Profiler.AddPushButton(new PushButtonData("Btn_RoofFromFloor_V07", "Roof From Floor V07 (Converts Curve to lines)", assemblyPath, "Revit26_Plugin.RoofFromFloor.V007.Commands.LaunchRoofFromFloorCommand"));
            Profiler.AddPushButton(new PushButtonData("Btn_RoofFromFloor_V08", "Roof From Floor V08 Fix ONe)", assemblyPath, "Revit26_Plugin.RoofFromFloor.V008.Commands.LaunchRoofFromFloorCommand"));
            Profiler.AddPushButton(new PushButtonData("Btn_RoofFromFloor_V09", "Roof From Floor V09 Circle Solved)", assemblyPath, "Revit26_Plugin.RoofFromFloor.V009.Commands.LaunchRoofFromFloorCommand"));
            Profiler.AddPushButton(new PushButtonData("Btn_RoofFromFloor_V10", "Roof From Floor 10 New WIP ***)", assemblyPath, "Revit26_Plugin.RoofFromFloor.V010.Commands.LaunchRoofFromFloorCommand"));

            Profiler.AddPushButton(new PushButtonData("Btn_MechanicalCircles_V007", "Mechanical Circles V00 7", assemblyPath, "Revit26_Plugin.LinesFromMechanical.V007.Commands.CreateLinkedMechanicalCirclesCommand"));
            Profiler.AddPushButton(new PushButtonData("Btn_MechanicalCircles_V009", "Mechanical Circles V009", assemblyPath, "Revit26_Plugin.LinesFromMechanical.V009.Commands.CreateLinkedMechanicalCirclesCommand"));
            Profiler.AddPushButton(new PushButtonData("Btn_MechanicalCircles_V010", "Mechanical Circles V010", assemblyPath, "Revit26_Plugin.LinesFromMechanical.V010.Commands.CreateLinkedMechanicalCirclesCommand"));

            //RoofConverter.AddPushButton(new PushButtonData("Btn_RoomToFloor", "Room To Floor", assemblyPath, "Revit22_Plugin.RoomToFloorCommand"));
            //RoofConverter.AddPushButton(new PushButtonData("Btn_RoomToRoof", "Room To Roof", assemblyPath, "Revit22_Plugin.RoomToRoofCommand"));


        }
    }
}