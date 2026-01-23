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

            SlopeMenu.AddPushButton(new PushButtonData("Btn_DijkstraPath2_2026", "DijkstraPath2_2026(Point)", assemblyPath, "Revit26_Plugin.Commands.DijkstraPath2_2026"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_RoofSloperClassic1_v2", "RoofSloperClassic1_v2(Point)", assemblyPath, "Revit26_Plugin.Commands.RoofSloperClassic1_v2"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_RoofSloperClassic_02", "RoofSloperClassic_02(Point)", assemblyPath, "Revit26_Plugin.Commands.RoofSloperClassic_02"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeCommand_03", "AutoSlopeCommand_03(Point)", assemblyPath, "Revit22_Plugin.AutoSlopeV3.AutoSlopeCommand_03"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeCommand_03_04", "AutoSlopeCommand_03_04(Point)", assemblyPath, "Revit26_Plugin.AutoSlopeByPoint.Commands.AutoSlopeCommand"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSloperDrain_04", "AutoSloperDrain_04(Drain)", assemblyPath, "Revit22_Plugin.Asd.Commands.AutoSloperDrain_04"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSloperDrain_04_01", "AutoSloperDrain_04_01(Drain)", assemblyPath, "Revit22_Plugin.Asd_V4_01.Commands.AutoSloperDrain_04_01"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlope04_02", "AutoSlope04_02(Drain)", assemblyPath, "Revit22_Plugin.V4_02.Commands.AutoSlopeCommand"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSloperAndDetail", "AutoSloperAndDetail", assemblyPath, "Revit26_Plugin.V5_00.Commands.AutoSlopeCommand"));
           
            PulldownButton ShapepointMenu = panel.AddItem(new PulldownButtonData("ShapepointMenu", "Shape Points")) as PulldownButton;
            ShapepointMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Shapepoints32.png");

            ShapepointMenu.AddPushButton(new PushButtonData("Btn_RoofCircleDividerV1", "Circle Divider 1.0", assemblyPath, "Revit22_Plugin.PDCV1.Commands.RoofLoopAnalyzerCommand_01"));//Working
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_AddRidgeAllPointsCommand_01", "AddRidgeAllPointsCommand 3.0", assemblyPath, "Revit22_Plugin.RPD.Commands.AddRidgeAllPointsCommand_03"));//Worki
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_RoofRidgePointsCommand", "Roof Ridge Points Command 4.0", assemblyPath, "Revit22_Plugin.Commands.RoofRidgePointsCommand04"));//Worki
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_AddRidgePoints", " Ridge Points by DL & Distance 5.0", assemblyPath, "Revit_26.CornertoDrainArrow.RoofDrainageCommand"));//Worki.

            PulldownButton LineAndPoint = panel.AddItem(new PulldownButtonData("LineAndPointMenu", "Line & PointMenu")) as PulldownButton;
            LineAndPoint.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Linematch32.png");

            //LineAndPointMenu.AddPushButton(new PushButtonData("Btn_RoofRidgeCommand_03", "RoofRidgeCommand_03", assemblyPath, "Revit22_Plugin.RRLPV3.Commands.RoofRidgeCommand_03"));//Working
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeCommand_V11", "Ridge By Two Points_V11", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.Commands.RoofRidgeCommand_V11"));//Working
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V12", "Roof Ridge&Lines v12", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Commands.RoofRidgeCommand_V12"));
            LineAndPoint.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V13", "Slope Lines From Roof Crease 13 ", assemblyPath, "Revit26_Plugin.CreaserAdv_V002.Commands.CreaserAdvCommand"));



            PulldownButton SlopeLinerMenu = panel.AddItem(new PulldownButtonData("SlopeLiner", "SlopeLiner")) as PulldownButton;
            SlopeLinerMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Addlines_32.png");

            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserCommand_03_01", "CreaserCommand V03_01", assemblyPath, "Revit26_Plugin.Creaser_V03_01.Commands.CreaserCommand"));
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_RoofDrainageCommand", "RoofDrainageCommand", assemblyPath, "Revit_26.CornertoDrainArrow_V05.RoofDrainageCommand"));                       
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V00", "CreaserAdvCommand V00 # NEW WIP ", assemblyPath, "Revit26_Plugin.CreaserAdv_V00.Commands.CreaserAdvCommand"));
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V001", "CreaserAdvCommand V001 # old", assemblyPath, "Revit26_Plugin.Creaser_adv_V001.Commands.RunCreaserAdvCommand"));
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_V002", "CreaserAdvCommand V002 # Working", assemblyPath, "Revit26_Plugin.CreaserAdv_V002.Commands.CreaserAdvCommand"));

            PulldownButton tagMenu = panel.AddItem(new PulldownButtonData("RoofTagMenu", "Tag")) as PulldownButton;
            tagMenu.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Addtag32.png");
            //tagMenu.LargeImage = IconManager.ToBitmapSource(Properties.Resources.addtag32);
            //tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagAll", "Tag All Roof Points 1.0", assemblyPath, "Revit22_Plugin.WorksetFromLinked.Commands.CreateWorksetsFromLinkedFiles"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommandV3", "Roof Tag Command V3", assemblyPath, "Revit22_Plugin.RoofTagV3.RoofTagCommandV3"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommandV4", "Roof Tag Command V4", assemblyPath, "Revit22_Plugin.RoofTagV4.RoofTagCommandV4"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommandV7", "RoofTagCommandV7", assemblyPath, "Revit22_Plugin.RoofTagV7.Commands.RoofTagCommandV7"));
            tagMenu.AddPushButton(new PushButtonData("Btn_DtlLineDimCommand_V03", "DtlLineDimCommand_V03", assemblyPath, "Revit26_Plugin.DtlLineDim_V03.Commands.SelectRoofAndPlaceEdgeDetailsCommand"));
            tagMenu.AddPushButton(new PushButtonData("Btn_CreateWorksetsFromLinkedFilesv02", "CreateWorksetsFromLinkedFilesv02", assemblyPath, "Revit26_Plugin.WSAV02.CreateWorksetsFromLinkedFilesv02"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommand_V61", "RoofTagCommand_V61", assemblyPath, "Revit26_Plugin.RoofTag_V61.Commands.RoofTagCommand_V61"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommandV3_00", "RoofTagCommandV3_00", assemblyPath, "Revit26_Plugin.RoofTag_V03.Commands.RoofTagCommandV3"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommand_v73", "RoofTagCommand_73 WIP", assemblyPath, "Revit26_Plugin.RoofTag_V73.Commands.RoofTagCommand"));
            //setup.AddPushButton(new PushButtonData("Btn_CreateWorksetsFromLinkedFilesv02", "Create Worksets FromLinked Files v02", assemblyPath, "Revit26_Plugin.WSAV02.CreateWorksetsFromLinkedFilesv02"));

            /*
           RoofConverter.AddPushButton(new PushButtonData("Btn_RoomToFloor", "Room To Floor", assemblyPath, "Revit22_Plugin.RoomToFloorCommand"));
           RoofConverter.AddPushButton(new PushButtonData("Btn_RoomToRoof", "Room To Roof", assemblyPath, "Revit22_Plugin.RoomToRoofCommand")); */


        }
    }
}
