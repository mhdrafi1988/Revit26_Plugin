using Autodesk.Revit.UI;
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
            //string Autoslope32 = "Revit26_Plugin.Resources.Icons.Autoslope32.png";
            PulldownButton SlopeMenu = panel.AddItem(new PulldownButtonData("RoofSlopeMenu", "Auto SLope")) as PulldownButton;
            //SlopeMenu.LargeImage = IconManager.ToBitmapSource(Properties.Resources.Autoslope32);
            //SlopeMenu.Image = IconLoader.LoadPng(Autoslope32);
            SlopeMenu.AddPushButton(new PushButtonData("Btn_DijkstraPath2_2026", "DijkstraPath2_2026(Point)", assemblyPath, "Revit26_Plugin.Commands.DijkstraPath2_2026"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_RoofSloperClassic1_v2", "RoofSloperClassic1_v2(Point)", assemblyPath, "Revit26_Plugin.Commands.RoofSloperClassic1_v2"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_RoofSloperClassic_02", "RoofSloperClassic_02(Point)", assemblyPath, "Revit26_Plugin.Commands.RoofSloperClassic_02"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeCommand_03", "AutoSlopeCommand_03(Point)", assemblyPath, "Revit22_Plugin.AutoSlopeV3.AutoSlopeCommand_03"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSloperDrain_04", "AutoSloperDrain_04(Drain)", assemblyPath, "Revit22_Plugin.Asd.Commands.AutoSloperDrain_04"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSloperDrain_04_01", "AutoSloperDrain_04_01(Drain)", assemblyPath, "Revit22_Plugin.Asd_V4_01.Commands.AutoSloperDrain_04_01"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlope04_02", "AutoSlope04_02(Drain)", assemblyPath, "Revit22_Plugin.V4_02.Commands.AutoSlopeCommand"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSloperAndDetail", "AutoSloperAndDetail", assemblyPath, "Revit26_Plugin.V5_00.Commands.AutoSlopeCommand"));
           
            PulldownButton ShapepointMenu = panel.AddItem(new PulldownButtonData("ShapepointMenu", "Shape Points")) as PulldownButton;
            //ShapepointMenu.LargeImage = IconManager.ToBitmapSource(Properties.Resources.addpoints32);
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_RoofCircleDividerV1", "Circle Divider 1.0", assemblyPath, "Revit22_Plugin.PDCV1.Commands.RoofLoopAnalyzerCommand_01"));//Working
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_AddRidgeAllPointsCommand_01", "AddRidgeAllPointsCommand 3.0", assemblyPath, "Revit22_Plugin.RPD.Commands.AddRidgeAllPointsCommand_03"));//Worki
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_RoofRidgePointsCommand", "Roof Ridge Points Command 4.0", assemblyPath, "Revit22_Plugin.Commands.RoofRidgePointsCommand04"));//Worki
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_AddRidgePoints", " Ridge Points by DL & Distance 5.0", assemblyPath, "Revit_26.CornertoDrainArrow.RoofDrainageCommand"));//Worki.

            PulldownButton LineAndPointMenu = panel.AddItem(new PulldownButtonData("LineAndPointMenu", "Line & PointMenu")) as PulldownButton;
            //LineAndPointMenu.AddPushButton(new PushButtonData("Btn_RoofRidgeCommand_03", "RoofRidgeCommand_03", assemblyPath, "Revit22_Plugin.RRLPV3.Commands.RoofRidgeCommand_03"));//Working
            LineAndPointMenu.AddPushButton(new PushButtonData("Btn_RoofRidgeCommand_V11", "RoofRidgeCommand_V11", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.Commands.RoofRidgeCommand_V11"));//Working
            LineAndPointMenu.AddPushButton(new PushButtonData("Btn_RoofRidgeLines_V12", "Roof Ridge&Lines v12", assemblyPath, "Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.Commands.RoofRidgeCommand_V12"));



            PulldownButton SlopeLinerMenu = panel.AddItem(new PulldownButtonData("SlopeLiner", "SlopeLiner")) as PulldownButton;            
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserCommand_03_01", "CreaserCommand V03_01", assemblyPath, "Revit26_Plugin.Creaser_V03_01.Commands.CreaserCommand"));
            //SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_RunCreaserAdvCommand", "RunCreaserAdvCommand", assemblyPath, "Revit26_Plugin.Creaser_adv_V001.Commands.RunCreaserAdvCommand"));
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_RoofDrainageCommand", "RoofDrainageCommand", assemblyPath, "Revit_26.CornertoDrainArrow_V05.RoofDrainageCommand"));
            //SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_RoofDrainageCommand", "RoofDrainageCommand #", assemblyPath, "Revit26_Plugin.Creaser_V32.Commands.CreaserCommand"));

            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_RunCreaserAdvCommand", "RunCreaserAdvCommand #", assemblyPath, "Revit26_Plugin.Creaser_adv_V001.Commands.RunCreaserAdvCommand"));
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_002", "CreaserAdvCommand_002 #", assemblyPath, "Revit26_Plugin.CreaserAdv_V002.Commands.CreaserAdvCommand"));
            SlopeLinerMenu.AddPushButton(new PushButtonData("Btn_CreaserAdvCommand_001", "CreaserAdvCommand_001 #", assemblyPath, "Revit26_Plugin.AutoLiner_V02.Commands.AutoLinerCommand_V02"));
            





            PulldownButton tagMenu = panel.AddItem(new PulldownButtonData("RoofTagMenu", "Tag")) as PulldownButton;
           //tagMenu.LargeImage = IconManager.ToBitmapSource(Properties.Resources.addtag32);


           //tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagAll", "Tag All Roof Points 1.0", assemblyPath, "Revit22_Plugin.WorksetFromLinked.Commands.CreateWorksetsFromLinkedFiles"));
           
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommandV3", "Roof Tag Command V3", assemblyPath, "Revit22_Plugin.RoofTagV3.RoofTagCommandV3"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommandV4", "Roof Tag Command V4", assemblyPath, "Revit22_Plugin.RoofTagV4.RoofTagCommandV4"));
            tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommandV7", "RoofTagCommandV7", assemblyPath, "Revit22_Plugin.RoofTagV7.Commands.RoofTagCommandV7"));



            tagMenu.AddPushButton(new PushButtonData("Btn_DtlLineDimCommand_V03", "DtlLineDimCommand_V03", assemblyPath, "Revit26_Plugin.DtlLineDim_V03.Commands.SelectRoofAndPlaceEdgeDetailsCommand"));

            tagMenu.AddPushButton(new PushButtonData("Btn_CreateWorksetsFromLinkedFilesv02", "CreateWorksetsFromLinkedFilesv02", assemblyPath, "Revit26_Plugin.WSAV02.CreateWorksetsFromLinkedFilesv02"));

            //setup.AddPushButton(new PushButtonData("Btn_CreateWorksetsFromLinkedFilesv02", "Create Worksets FromLinked Files v02", assemblyPath, "Revit26_Plugin.WSAV02.CreateWorksetsFromLinkedFilesv02"));

            /*
           RoofConverter.AddPushButton(new PushButtonData("Btn_RoomToFloor", "Room To Floor", assemblyPath, "Revit22_Plugin.RoomToFloorCommand"));
           RoofConverter.AddPushButton(new PushButtonData("Btn_RoomToRoof", "Room To Roof", assemblyPath, "Revit22_Plugin.RoomToRoofCommand")); */


        }
    }
}
