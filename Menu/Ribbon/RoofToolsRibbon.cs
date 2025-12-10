using Autodesk.Revit.UI;
//using Revit22_Plugin.Commands;
//using Revit22_Plugin.Utils;
using System;
using System.Reflection;
using System.Windows.Markup;
//using System.Windows.Media.Imaging;

namespace Revit26_Plugin.Ribbon
{
    public static class RoofToolsRibbon
    {
        //Bitmaps
       /* private static BitmapSource LoadPng(string relativePath)
        {
            string assemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            string uriPath = $"pack://application:,,,/{assemblyName};component/{relativePath}";
            return new BitmapImage(new Uri(uriPath));
        }*/
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = app.CreateRibbonPanel(tabName, "Roof Tools");

            PulldownButton SlopeMenu = panel.AddItem(new PulldownButtonData("RoofSlopeMenu", "Auto SLope")) as PulldownButton;
            //SlopeMenu.LargeImage = IconManager.ToBitmapSource(Properties.Resources.Autoslope32);
            
            SlopeMenu.AddPushButton(new PushButtonData("Btn_DijkstraPath2_2026", "DijkstraPath2_2026(Point)", assemblyPath, "Revit26_Plugin.Commands.DijkstraPath2_2026"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_RoofSloperClassic1_v2", "RoofSloperClassic1_v2(Point)", assemblyPath, "Revit26_Plugin.Commands.RoofSloperClassic1_v2"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_RoofSloperClassic_02", "RoofSloperClassic_02(Point)", assemblyPath, "Revit26_Plugin.Commands.RoofSloperClassic_02"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeCommand_03", "AutoSlopeCommand_03(Point)", assemblyPath, "Revit22_Plugin.AutoSlopeV3.AutoSlopeCommand_03"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSloperDrain_04", "AutoSloperDrain_04(Drain)", assemblyPath, "Revit22_Plugin.Asd.Commands.AutoSloperDrain_04"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSloperDrain_04_01", "AutoSloperDrain_04_01(Drain)", assemblyPath, "Revit22_Plugin.Asd_V4_01.Commands.AutoSloperDrain_04_01"));
            
            /*

            PulldownButton LineMenu = panel.AddItem(new PulldownButtonData("addlineMenu", "Add Lines")) as PulldownButton;
            //LineMenu.LargeImage = IconManager.ToBitmapSource(Properties.Resources.addlines32);

            LineMenu.AddPushButton(new PushButtonData("Btn_RoofRidgeLinepoints_01", "Detail Line Connection 2.0", assemblyPath, "Revit22_Plugin.Commands.RoofRidgeLineandPoints_01"));
            LineMenu.AddPushButton(new PushButtonData("Btn_RoofRidgeLineandPointsV1", "Detail Line Connection 2.0", assemblyPath, "Revit22_Plugin.Commands.RoofRidgeLineandPointsV1"));            
            LineMenu.AddPushButton(new PushButtonData("Btn_RoofRidgeLinepoints_03", "Perpendicular & Points 3.0", assemblyPath, "Revit22_Plugin.RRLPV3.Commands.RoofRidgeCommand_03"));
            LineMenu.AddPushButton(new PushButtonData("Btn_RoofRidgeLineandPoints_04", "Roof Ridge Lineand Points_V1", assemblyPath, "Revit22_Plugin.Commands.RoofRidgeLineandPointsV1")); */

            PulldownButton ShapepointMenu = panel.AddItem(new PulldownButtonData("ShapepointMenu", "Shape Points")) as PulldownButton;
            //ShapepointMenu.LargeImage = IconManager.ToBitmapSource(Properties.Resources.addpoints32);
            //ShapepointMenu.AddPushButton(new PushButtonData("Btn_AddShapePointsFromLines", "Add Shape Points From Lines On roof Edge 1.0", assemblyPath, "Revit22_Plugin.Commands.AddRidgeAllPointsCommand_01"));//working-need some updation notify to select roof and multiple lines
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_RoofCircleDividerV1", "Circle Divider 1.0", assemblyPath, "Revit22_Plugin.PDCV1.Commands.RoofLoopAnalyzerCommand_01"));//Working
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_AddRidgeAllPointsCommand_01", "AddRidgeAllPointsCommand 3.0", assemblyPath, "Revit22_Plugin.RPD.Commands.AddRidgeAllPointsCommand_03"));//Worki
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_RoofRidgePointsCommand", "Roof Ridge Points Command 4.0", assemblyPath, "Revit22_Plugin.Commands.RoofRidgePointsCommand04"));//Worki
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_AddRidgePoints", " Ridge Points by DL & Distance 5.0", assemblyPath, "Revit22_Plugin.Commands.AddRidgePointsDistance"));//Worki



            /*PulldownButton tagMenu = panel.AddItem(new PulldownButtonData("RoofTagMenu", "Tag")) as PulldownButton;
           //tagMenu.LargeImage = IconManager.ToBitmapSource(Properties.Resources.addtag32);

           tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagAll", "Tag All Roof Points 1.0", assemblyPath, "Revit22_Plugin.RoofTagCommand"));
           tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagSelectedv2", "Tag Selected Vertices 2.0", assemblyPath, "Revit22_Plugin.RoofTagCommandSelected"));
           tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommandV3", "RoofTagCommand V3.0", assemblyPath, "Revit22_Plugin.RoofTagV3.RoofTagCommandV3"));
           tagMenu.AddPushButton(new PushButtonData("Btn_RoofTagCommandV4", "RoofTagCommand V4.0", assemblyPath, "Revit22_Plugin.RoofTagV4.RoofTagCommandV4"));


           PulldownButton RoofConverter = panel.AddItem(new PulldownButtonData("RoofConverter", "RoofConverter")) as PulldownButton;
          // RoofConverter.LargeImage = IconManager.ToBitmapSource(Properties.Resources.converter32);

           RoofConverter.AddPushButton(new PushButtonData("Btn_RoomToFloor", "Room To Floor", assemblyPath, "Revit22_Plugin.RoomToFloorCommand"));
           RoofConverter.AddPushButton(new PushButtonData("Btn_RoomToRoof", "Room To Roof", assemblyPath, "Revit22_Plugin.RoomToRoofCommand")); */


        }
    }
}
