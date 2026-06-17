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
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint_07", "AutoSlopeByPoint_07_New Dijkstra", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint.V07.Commands.AutoSlopeCommand"));

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

            ShapepointMenu.AddPushButton(new PushButtonData("Btn_RoofCircleDividerV1", "Inner Loop Divider 1.0", assemblyPath, "Revit22_Plugin.PDCV1.Commands.RoofLoopAnalyzerCommand_01"));//Working
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_PDCV2", "Inner Loop Divider 2.0", assemblyPath, "Revit26_Plugin.PDCV2.Commands.RoofLoopAnalyzerCommand"));//Working
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_PDCV3", "Outer Loop Divider 3.0", assemblyPath, "Revit26_Plugin.PDCV3.Commands.RoofLoopAnalyzerCommand"));//Working
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_PonitOnCurvesInnerandOuter", "PonitOnCurvesInnerandOuter", assemblyPath, "Revit26_Plugin.PonitOnCurvesInnerandOuter.V01.Commands.RoofLoopAnalyzerCommand"));//Working
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_PointOnBoundary", "Point on  Boundry(Roof & Detail LIne)", assemblyPath, "Revit26_Plugin.AddPointOnintersections.Commands.AddPointOnIntersectionsCommand"));

            PulldownButton LineAndPoint = panel.AddItem(new PulldownButtonData("LineAndPointMenu", "Line & PointMenu")) as PulldownButton;
            LineAndPoint.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Linematch32.png");

            
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V11", "Auto Ridger(Multiple Shapes)11", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.Commands.RoofRidgeCommand_V11"));//Working
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V51", "Auto Ridger(Multiple Shapes)51", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V51.Commands.RoofRidgeCommand"));//Working
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V52", "Auto Ridger(Multiple Shapes)52", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V52.Commands.RoofRidgeCommand"));//Working
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V53", "Auto Ridger(Multiple Shapes)53", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V53.Commands.RoofRidgeCommand"));//Working
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V54", "Auto Ridger(Multiple Shapes)54***", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V54.Commands.RoofRidgeCommand"));//Working
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V55", "Auto Ridger(Multiple Shapes)55***", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines.V55.Commands.RoofRidgeCommand"));//Working

            PulldownButton SlopeLinerMenu = panel.AddItem(new PulldownButtonData("SlopeLiner", "SlopeLiner")) as PulldownButton;
            SlopeLinerMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Addlines_32.png");

            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V00", "CreaserAdvCommand V00 # NEW WIP ", assemblyPath, "Revit26_Plugin.CreaserAdv_V00.Commands.CreaserAdvCommand"));
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V001", "CreaserAdvCommand V001", assemblyPath, " Revit26_Plugin.AutoLiner_V01.Commands.AutoLinerCommand_V01"));
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V002", "CreaserAdvCommand V002 # Working", assemblyPath, "Revit26_Plugin.CreaserAdv_V002.Commands.CreaserAdvCommand"));
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V71", "CreaserAdvCommand V71 # Working", assemblyPath, "Revit26_Plugin.CreaserAdv.V071.Commands.CreaserAdvCommand"));

            

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
                                    
           //RoofConverter.AddPushButton(new PushButtonData("Btn_RoomToFloor", "Room To Floor", assemblyPath, "Revit22_Plugin.RoomToFloorCommand"));
           //RoofConverter.AddPushButton(new PushButtonData("Btn_RoomToRoof", "Room To Roof", assemblyPath, "Revit22_Plugin.RoomToRoofCommand"));


        }
    }
}