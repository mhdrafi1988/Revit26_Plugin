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

            //BY Point
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeCommand_03_07", "AutoSlopeCommand_03_07(Point)**", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint_30_07.Commands.AutoSlopeCommand"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint_04", "AutoSlopeByPoint_00_04_CSV(Classic)", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint_04.Commands.AutoSlopeCommand"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint", "AutoSlopeByPoint_00_00(Classic)", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint.Commands.AutoSlopeCommand"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_DijkstraPath2_2026", "DijkstraPath2_2026(Point)", assemblyPath, "Revit26_Plugin.Commands.DijkstraPath2_2026"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_RoofSloperClassic1_v2", "RoofSloperClassic1_V2_CSV(Point)", assemblyPath, "Revit26_Plugin.Commands.RoofSloperClassic1_v2"));
            
            //BY Drain 
            //SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSloperDrain_04_00", "AutoSloperDrain_04_00(Drain*)", assemblyPath, "Revit22_Plugin.Asd.Commands.AutoSloperDrain_04"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSloperDrain_Asd_19", "AutoSloperDrain_Asd_19_CSV(Drain)", assemblyPath, "Revit26_Plugin.Asd_19.Commands.AutoSloperDrain_04"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSloperDrain_04_01", "AutoSloperDrain_04_01(Drain)", assemblyPath, "Revit22_Plugin.Asd_V4_01.Commands.AutoSloperDrain_04_01"));
            //And Detail
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSloperAndDetail", "AutoSloperAndDetail", assemblyPath, "Revit26_Plugin.V5_00.Commands.AutoSlopeCommand"));
            SlopeMenu.AddPushButton(new PushButtonData("LaunchRoofFromFloorCommand", "LaunchRoofFromFloorCommand", assemblyPath, "Revit26_Plugin.RoofFromFloor.Commands.LaunchRoofFromFloorCommand"));

            PulldownButton ShapepointMenu = panel.AddItem(new PulldownButtonData("ShapepointMenu", "Shape Points")) as PulldownButton;
            ShapepointMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Shapepoints32.png");

            ShapepointMenu.AddPushButton(new PushButtonData("Btn_RoofCircleDividerV1", "Circle Divider 1.0", assemblyPath, "Revit22_Plugin.PDCV1.Commands.RoofLoopAnalyzerCommand_01"));//Working
            
            PulldownButton LineAndPoint = panel.AddItem(new PulldownButtonData("LineAndPointMenu", "Line & PointMenu")) as PulldownButton;
            LineAndPoint.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Linematch32.png");

            //LineAndPointMenu.AddPushButton(new PushButtonData("Btn_RoofRidgeCommand_03", "RoofRidgeCommand_03", assemblyPath, "Revit22_Plugin.RRLPV3.Commands.RoofRidgeCommand_03"));//Working
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeCommand_V11", "Ridge Points & Lines By Two Points_V11", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.Commands.RoofRidgeCommand_V11"));//Working
            
            PulldownButton SlopeLinerMenu = panel.AddItem(new PulldownButtonData("SlopeLiner", "SlopeLiner")) as PulldownButton;
            SlopeLinerMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Addlines_32.png");
            
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V00", "CreaserAdvCommand V00 # NEW WIP ", assemblyPath, "Revit26_Plugin.CreaserAdv_V00.Commands.CreaserAdvCommand"));
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V002", "CreaserAdvCommand V002 # Working", assemblyPath, "Revit26_Plugin.CreaserAdv_V002.Commands.CreaserAdvCommand"));

            PulldownButton tagMenu = panel.AddItem(new PulldownButtonData("RoofTagMenu", "Tag")) as PulldownButton;
            tagMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Addtag32.png");
            //tagMenu.LargeImage = IconManager.ToBitmapSource(Properties.Resources.addtag32);
                                  
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTag_V03", "RoofTag_V03", assemblyPath, "Revit26_Plugin.RoofTag_V03.Commands.RoofTagCommandV3"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommandV3", "RoofTagCommandV3", assemblyPath, "Revit22_Plugin.RoofTag_V90.RoofTagCommandV3"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommandV3.1", "RoofTagCommandV3", assemblyPath, "Revit26_Plugin.RoofTag_V03.Commands.RoofTagCommandV3"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommand_V5)", "RoofTagCommand V5", assemblyPath, "Revit26_Plugin.RoofTag_V73.Commands.RoofTagCommand"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommand_V6)", "RoofTagCommand V6", assemblyPath, "Revit26_Plugin.RoofTag_V03.Commands.RoofTagCommandV3"));            

            //setup.AddPushButton(new PushButtonData("Btn_CreateWorksetsFromLinkedFilesv02", "Create Worksets FromLinked Files v02", assemblyPath, "Revit26_Plugin.WSAV02.CreateWorksetsFromLinkedFilesv02"));

            /*
           RoofConverter.AddPushButton(new PushButtonData("Btn_RoomToFloor", "Room To Floor", assemblyPath, "Revit22_Plugin.RoomToFloorCommand"));
           RoofConverter.AddPushButton(new PushButtonData("Btn_RoomToRoof", "Room To Roof", assemblyPath, "Revit22_Plugin.RoomToRoofCommand")); */


        }
    }
}
