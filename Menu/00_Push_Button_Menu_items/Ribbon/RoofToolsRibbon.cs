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
             
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint_025", "Auto Slope ByPoint_025 (Final) ", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint.V025.Commands.AutoSlopeCommand"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint_026", "Auto Slope ByPoint_026 (WIP ##)", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint.V026.Commands.AutoSlopeCommand"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint_027", "Auto Slope ByPoint_027 (WIP ##)", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint.V0027.Commands.AutoSlopeCommand"));


            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint.WithRidge V001", "Auto Slope By Point With Ridge V001", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint.WithRidge.Commands.AutoSlopeCommand"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPointTwoSlopes_01_00", "Auto Slope By Point Two Slopes 01_00 Excel(WIP)", assemblyPath, "AutoSlopeByPointTwoSlopes_01_00.Commands.AutoSlopeCommand"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPointTwoSlopes_02_00", "Auto Slope By Point Two Slopes 02_00 Excel(WIP)", assemblyPath, "Revit26_Plugin.AutoSlopeByPointTwoSlopes.V002.AutoSlopeCommand"));

            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint.RPF_001", "Auto Slope By Point RPF_001 ####", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint.RPF_001.Commands.AutoSlopeCommand"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint.RPF_002", "Auto Slope By Point RPF_002 ####", assemblyPath, "Revit26_Plugin.AutoSlopeByPointRPF.V002.RPF_001.Commands.AutoSlopeCommand"));


            //Slope BY Drain 

            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSloperDrain_Asd_19", "Auto Sloper Drain Asd_19 CSV(Drain)", assemblyPath, "Revit26_Plugin.Asd_19.Commands.AutoSloperDrain_04"));            
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByDrain_V005", "Auto Slope By Drain V005 (Drain)##", assemblyPath, "Revit26_Plugin.AutoSlopeByDrain.V005.Commands.AutoSlopeByDrain"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByDrain_V006", "Auto Slope By Drain V006 (Drain)##", assemblyPath, "Revit26_Plugin.AutoSlopeByDrain.V006.Commands.AutoSlopeByDrain"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_RoofSloperClassic1_V001", "Roof Sloper Classic1 V001", assemblyPath, "Revit26_Plugin.RoofSloperClassic1.V001.RoofSloperClassic1_v2"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_SlopeDirections_V005", "Slope Directions V005", assemblyPath, "Revit26_Plugin.SlopeDirections.V005.RoofDrainageCommand"));

            //Create Shape Point Shape Point Shape Point Shape Point

            PulldownButton ShapepointMenu = panel.AddItem(new PulldownButtonData("ShapepointMenu", "Shape Points")) as PulldownButton;
            ShapepointMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Shapepoints32.png");
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_ADivideInnerLoops_V006", "Divide Inner Loops   V006", assemblyPath, "Revit26_Plugin.DivideInnerLoops.V006.RoofLoopAnalyzerCommand"));//Working
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_ADivideInnerLoops_V007", "Divide Inner Loops   V007", assemblyPath, "Revit26_Plugin.InnerLoopDivider.V007.RoofLoopAnalyzerCommand"));

            //ShapepointMenu.AddPushButton(new PushButtonData("Btn_ADivideInnerLoops_V002", "Divide Inner Loops_PDCV2 ", assemblyPath, "Revit26_Plugin.PDCV2.Commands.RoofLoopAnalyzerCommand"));//Working 
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_AddPointOnIntersectionsCommand 05", "Outer CurveDivider.V001", assemblyPath, "Revit26_Plugin.OuterCurveDivider.V001.Commands.CurveDividerCommand"));//Working
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_OuterCurveDivider_V002", "Outer CurveDivider.V002", assemblyPath, "Revit26_Plugin.OuterCurveDivider.V002.Commands.CurveDividerCommand"));
            
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_RoofDetailLineIntersect 08", "Roof Detail Line Intersect V0008 #", assemblyPath, "Revit26_Plugin.RoofDetailLineIntersect.V008.RoofDetailLineIntersectCommand"));//Working
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_RoofDetailLineIntersect 09", "Roof Detail Line Intersect V0009 #", assemblyPath, "Revit26_Plugin.RoofDetailLineIntersect.V009.RoofDetailLineIntersectCommand"));

            ShapepointMenu.AddPushButton(new PushButtonData("Btn_PonitOnCurvesInnerandOuter 01", "Ponits On Curves Inner & Outer V02", assemblyPath, "Revit26_Plugin.PonitOnCurvesInnerandOuter.V01.Commands.RoofLoopAnalyzerCommand"));//Working
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_PonitOnCurvesInnerandOuter_V003", "Ponits On Curves Inner & Outer V03", assemblyPath, "Revit26_Plugin.PonitOnCurvesInnerandOuter.V003.Commands.RoofLoopAnalyzerCommand"));

            ShapepointMenu.AddPushButton(new PushButtonData("Btn_DivideInnerLoopsAndPerpendicular_V002", "DivideInnerLoopsAndPerpendicular V002##", assemblyPath, "Revit26_Plugin.DivideInnerLoopsAndPerpendicular.V002.PerpendicularPointCommand"));//Working
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_DivideInnerLoopsAndPerpendicular_V003", "DivideInnerLoopsAndPerpendicular V003##", assemblyPath, "Revit26_Plugin.InnerLoopsAndPerpendicular.V003.PerpendicularPointCommand"));

            ShapepointMenu.AddPushButton(new PushButtonData("Btn_RoofEdgeVertexReducer V03", "RoofEdgeVertexReducer V03", assemblyPath, "Revit26_Plugin.RoofEdgeVertexReducer.V003.Commands.RoofEdgeVertexReducer"));
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_VertexReducer_V005", "RoofEdgeVertexReducer V03 V005", assemblyPath, "Revit26_Plugin.VertexReducer.V005.Commands.RoofEdgeVertexReducer"));

            PulldownButton LineAndPoint = panel.AddItem(new PulldownButtonData("LineAndPointMenu", "Line & PointMenu")) as PulldownButton;
            LineAndPoint.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Linematch32.png");

            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V57", "Auto Ridger(Multiple Shapes)57(By Point)", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V057.Commands.RoofRidgeCommand"));//Working
           
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V67", "Auto Ridger(Multiple Shapes)67(By Shape)", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V67.Commands.RoofRidgeCommand"));//Working
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V68", "Auto Ridger(Multiple Shapes)68(By Shape)", assemblyPath, "Revit26_Plugin.RoofRidgeLines.V068.Commands.RoofRidgeCommand"));

                        //Slope Liner Menu
            PulldownButton SlopeLinerMenu = panel.AddItem(new PulldownButtonData("SlopeLiner", "SlopeLiner")) as PulldownButton;
            SlopeLinerMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Addlines_32.png");

            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V002_01", "CreaserAdvCommand V002_01 # Working", assemblyPath, "Revit26_Plugin.CreaserAdv_V002_01.Commands.CreaserAdvCommand"));
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V003_01", "CreaserAdvCommand V003_01 # Working", assemblyPath, "Revit26_Plugin.AutoCreaser.V003.Commands.CreaserAdvCommand"));
            
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V007_00", "CreaserAdvCommand V007_00 # Working", assemblyPath, "Revit26_Plugin.CreaserAdv_V007_00.Commands.CreaserAdvCommand"));
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V008_00", "CreaserAdvCommand V008_00 # Working", assemblyPath, "Revit26_Plugin.CreaserAdv_V008_00.Commands.CreaserAdvCommand"));
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V009_00", "CreaserAdvCommand V009_00 # Working", assemblyPath, "Revit26_Plugin.CreaserAdv.V009.Commands.CreaserAdvCommand"));


            PulldownButton tagMenu = panel.AddItem(new PulldownButtonData("RoofTagMenu", "Tag")) as PulldownButton;
            tagMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Addtag32.png");
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommand_V007A)", "RoofTagCommand V007 A Face Ref", assemblyPath, "Revit26_Plugin.RoofTag_V007_A.RoofTagCommand"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommand_V008)", "RoofTagCommand V008", assemblyPath, "Revit26_Plugin.RoofTag_V008.RoofTagCommand"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommand_V014)", "RoofTagCommand V014", assemblyPath, "Revit26_Plugin.RoofTag_V014.RoofTagCommand"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommand_V015)", "RoofTagCommand V015", assemblyPath, "Revit26_Plugin.RoofTag_V015.RoofTagCommand"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommand_V016)", "RoofTagCommand V016", assemblyPath, "Revit26_Plugin.RoofTag.V016.RoofTagCommand"));

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
            Profiler.AddPushButton(new PushButtonData("Btn_MechanicalCircles_V011", "Mechanical Circles V011", assemblyPath, "Revit26_Plugin.LinesFromMechanical.V011.Commands.CreateLinkedMechanicalCirclesCommand"));

            PulldownButton CreateMenu = panel.AddItem(new PulldownButtonData("RoofCreateMenu", "Create")) as PulldownButton;
            CreateMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.CreateRoofromLInes.png");
            //CreateMenu.LargeImage = IconManager.ToBitmapSource(Properties.Resources.addtag32);
                        
            CreateMenu.AddPushButton(new PushButtonData("Btn_RoofFromDetailLines.V007", "Roof From Detail Lines_V007", assemblyPath, "Revit26_Plugin.RoofFromDetailLines.V007.Command"));




        }
    }
}