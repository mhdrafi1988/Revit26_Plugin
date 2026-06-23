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

            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint_05", "AutoSlopeByPoint_05", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint_05.Commands.AutoSlopeCommand"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint_06", "AutoSlopeByPoint_06_New Dijkstra", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint.V06.Commands.AutoSlopeCommand"));

            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint", "AutoSlopeByPoint_00_00(Classic)", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint.Commands.AutoSlopeCommand"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint_04", "AutoSlope(ByPoint)00_04_Excel(Classic)", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint_04.Commands.AutoSlopeCommand"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPointTwoSlopes_01_00", "AutoSlopeByPointTwoSlopes_01_00_Excel(WIP)", assemblyPath, "AutoSlopeByPointTwoSlopes_01_00.Commands.AutoSlopeCommand"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_DijkstraPath2_2026", "DijkstraPath2_2026(Point)", assemblyPath, "Revit26_Plugin.Commands.DijkstraPath2_2026"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_RoofSloperClassic1_v2", "RoofSloperClassic1_V2_CSV(Point)", assemblyPath, "Revit26_Plugin.Commands.RoofSloperClassic1_v2"));
            
            //Slope BY Drain 
            
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSloperDrain_Asd_19", "AutoSloperDrain_Asd_19_CSV(Drain)", assemblyPath, "Revit26_Plugin.Asd_19.Commands.AutoSloperDrain_04"));
            
            //Create RoofAnd Detail
            
            PulldownButton ShapepointMenu = panel.AddItem(new PulldownButtonData("ShapepointMenu", "Shape Points")) as PulldownButton;
            ShapepointMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Shapepoints32.png");


            ShapepointMenu.AddPushButton(new PushButtonData("Btn_AddPointOnIntersectionsCommand 01", "Divide Inner Loops", assemblyPath, "Revit22_Plugin.PDCV1.Commands.RoofLoopAnalyzerCommand_01"));//Working
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_AddPointOnIntersectionsCommand 06", "Divide Inner Loops V0002", assemblyPath, "RRevit26_Plugin.Tools.DivideInnerLoops.V002.RoofLoopAnalyzerCommand"));//Working

            
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_AddPointOnIntersectionsCommand 02", "Ponits On Curves Inner & Outer V02", assemblyPath, "Revit26_Plugin.PonitOnCurvesInnerandOuter.V01.Commands.RoofLoopAnalyzerCommand"));//Working
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_AddPointOnIntersectionsCommand 03", "Roof Loop Analyzer_PDCV2 (2)", assemblyPath, "Revit26_Plugin.PDCV2.Commands.RoofLoopAnalyzerCommand"));//Working
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_AddPointOnIntersectionsCommand 04", "Roof Loop Analyzer_PDCV3-Outer", assemblyPath, "Revit26_Plugin.PDCV3.Commands.RoofLoopAnalyzerCommand"));//Working
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_AddPointOnIntersectionsCommand 05", "Outer CurveDivider.V001", assemblyPath, "Revit26_Plugin.OuterCurveDivider.V001.Commands.CurveDividerCommand"));//Working



            PulldownButton LineAndPoint = panel.AddItem(new PulldownButtonData("LineAndPointMenu", "Line & PointMenu")) as PulldownButton;
            LineAndPoint.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Linematch32.png");


            
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V56", "Auto Ridger(Multiple Shapes)56(By Point)", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V56.Commands.RoofRidgeCommand"));//Working
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V60", "Auto Ridger(Multiple Shapes)60(By Shape)", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V60.Commands.RoofRidgeCommand"));//Working
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V62", "Auto Ridger(Multiple Shapes)62(By Shape)", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V62.Commands.RoofRidgeCommand"));//Working
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V64", "Auto Ridger(Multiple Shapes)64(By Shape)", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V64.Commands.RoofRidgeCommand"));//Working



            PulldownButton SlopeLinerMenu = panel.AddItem(new PulldownButtonData("SlopeLiner", "SlopeLiner")) as PulldownButton;
            SlopeLinerMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Addlines_32.png");

            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V002_01", "CreaserAdvCommand V002_01 # Working", assemblyPath, "Revit26_Plugin.CreaserAdv_V002_01.Commands.CreaserAdvCommand"));
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V003_01", "CreaserAdvCommand V003_01 # Working", assemblyPath, "Revit26_Plugin.CreaserAdv_V003_01.Commands.CreaserAdvCommand"));
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V004_00", "CreaserAdvCommand V004_00 # Working", assemblyPath, "Revit26_Plugin.CreaserAdv_V004_00.Commands.CreaserAdvCommand"));
            

            PulldownButton tagMenu = panel.AddItem(new PulldownButtonData("RoofTagMenu", "Tag")) as PulldownButton;
            tagMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Addtag32.png");
            //tagMenu.LargeImage = IconManager.ToBitmapSource(Properties.Resources.addtag32);

            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTag_V03", "RoofTag_V03", assemblyPath, "Revit26_Plugin.RoofTag_V03.Commands.RoofTagCommandV3"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommandV3", "RoofTagCommandV3", assemblyPath, "Revit22_Plugin.RoofTag_V90.RoofTagCommandV3"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommandV3.1", "RoofTagCommandV3", assemblyPath, "Revit26_Plugin.RoofTag_V03.Commands.RoofTagCommandV3"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommand_V5)", "RoofTagCommand V5", assemblyPath, "Revit26_Plugin.RoofTag_V73.Commands.RoofTagCommand"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommand_V6)", "RoofTagCommand V6", assemblyPath, "Revit26_Plugin.RoofTag_V03.Commands.RoofTagCommandV3"));

            PulldownButton Profiler = panel.AddItem(new PulldownButtonData("ProfilerTagMenu", "Profiler")) as PulldownButton;
            Profiler.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Addtag32.png");

            Profiler.AddPushButton(new PushButtonData("Btn_LaunchRoofFromFloorCommand", "LaunchRoofFromFloorCommand", assemblyPath, "Revit26_Plugin.RoofFromFloor.Commands.LaunchRoofFromFloorCommand"));
            Profiler.AddPushButton(new PushButtonData("Btn_CreateLinkedMechanicalCircles1", "CreateLinkedMechanicalCircles1", assemblyPath, "Revit26_Plugin.LinesFromMechanical.V001_01.Commands.CreateLinkedMechanicalCirclesCommand"));
            Profiler.AddPushButton(new PushButtonData("Btn_CreateLinkedMechanicalCircles2", "CreateLinkedMechanicalCircles2", assemblyPath, "Revit26_Plugin.LinesFromMechanical.V003.Commands.CreateLinkedMechanicalCirclesCommand"));
            Profiler.AddPushButton(new PushButtonData("Btn_CreateLinkedMechanicalCircles3", "CreateLinkedMechanicalCircles3", assemblyPath, "Revit26_Plugin.CreaserAdv.V071_02.Commands.CreaserAdvCommand"));

            Profiler.AddPushButton(new PushButtonData("Btn_LaunchRoofFromFloorCommand1", "LaunchRoofFromFloorCommand1", assemblyPath, "Revit26_Plugin.RoofFromFloor.Commands.LaunchRoofFromFloorCommand"));
            Profiler.AddPushButton(new PushButtonData("Btn_LaunchRoofFromFloorCommand V02", "LaunchRoofFromFloorCommand V02", assemblyPath, "Revit26_Plugin.RoofFromFloor.V02.LaunchRoofFromFloorCommand"));
            Profiler.AddPushButton(new PushButtonData("Btn_LaunchRoofFromFloorCommand V00", "LaunchRoofFromFloorCommand V00", assemblyPath, "Revit26_Plugin.RoofFromFloor.V00.LaunchRoofFromFloorCommand"));
            Profiler.AddPushButton(new PushButtonData("Btn_LaunchRoofFromFloorCommand V0005", "LaunchRoofFromFloorCommand V005", assemblyPath, "Revit26_Plugin.RoofFromFloor.V005.LaunchRoofFromFloorCommand"));
            Profiler.AddPushButton(new PushButtonData("Btn_LaunchRoofFromFloorCommand V0007", "LaunchRoofFromFloorCommand V007", assemblyPath, "Revit26_Plugin.RoofFromFloor.V007.LaunchRoofFromFloorCommand"));
            Profiler.AddPushButton(new PushButtonData("Btn_LaunchRoofFromFloorCommand V0008", "LaunchRoofFromFloorCommand V008", assemblyPath, "Revit26_Plugin.RoofFromFloor.V008.LaunchRoofFromFloorCommand"));









            //RoofConverter.AddPushButton(new PushButtonData("Btn_RoomToFloor", "Room To Floor", assemblyPath, "Revit22_Plugin.RoomToFloorCommand"));
            //RoofConverter.AddPushButton(new PushButtonData("Btn_RoomToRoof", "Room To Roof", assemblyPath, "Revit22_Plugin.RoomToRoofCommand"));


        }
    }
}