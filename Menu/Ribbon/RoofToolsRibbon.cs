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

            //SlopeMenu.AddPushButton(new PushButtonData("Btn_DijkstraPath2_2026", "Roof Sloper Classic_2026", assemblyPath, "Revit26_Plugin.Commands.DijkstraPath2_2026"));
            //SlopeMenu.AddPushButton(new PushButtonData("Btn_RoofDrainageDijkstra2026", "RoofDrainageDijkstra2026", assemblyPath, "Revit2026_Plugin.Commands.RoofDrainageDijkstra2026"));
            

            
            //SlopeMenu.AddPushButton(new PushButtonData("Btn_DijkstraPath2_2026", "DijkstraPath2_2026", assemblyPath, "Revit26_Plugin.Commands.DijkstraPath2_2026"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_DijkstraPath2_2026", "DijkstraPath2_2026", assemblyPath, "Revit26_Plugin.Commands.DijkstraPath2_2026"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_RoofSloperClassic1_v2", "RoofSloperClassic1_v2", assemblyPath, "Revit26_Plugin.Commands.RoofSloperClassic1_v2"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_RoofSloperClassic_02", "RoofSloperClassic_02", assemblyPath, "Revit26_Plugin.Commands.RoofSloperClassic_02"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSloperDrain_04", "AutoSloperDrain_04", assemblyPath, "Revit22_Plugin.Asd.Commands.AutoSloperDrain_04"));
            /*

            SlopeMenu.AddPushButton(new PushButtonData("Btn_DijkstraPath3", "Roof Sloper Classic 2.0", assemblyPath, "Revit2026_Plugin.Commands.RoofSloperClassic2_0"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSlopeCommand", "Auto RoofSloper (Points) 3.0", assemblyPath, "Revit2026_Plugin.AutoSlopeV11.Commands.AutoSlopeCommand_03"));//shred parameter updated
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSloperDrain_04 ", "AutoSloperDrain (Drains) 4.0", assemblyPath, "Revit22_Plugin.Asd.Commands.AutoSloperDrain_04"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSloperDrain_04_01", "AutoSloperDrain (Drains) 40_01", assemblyPath, "Revit22_Plugin.Asd_V4_01.Commands.AutoSloperDrainCommand"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_AutoSloperDrain_05 ", "Manual Drains SloperDrain 5.0", assemblyPath, "Revit22_Plugin.Commands.AutoSloper_05"));
            SlopeMenu.AddPushButton(new PushButtonData("Btn_DrainCutDetectorCommand_06 ", "Drain Detection 6.0", assemblyPath, "Revit22_Plugin.Commands.DrainCutDetector"));

            PulldownButton LineMenu = panel.AddItem(new PulldownButtonData("addlineMenu", "Add Lines")) as PulldownButton;
            //LineMenu.LargeImage = IconManager.ToBitmapSource(Properties.Resources.addlines32);

            LineMenu.AddPushButton(new PushButtonData("Btn_RoofRidgeLinepoints_01", "Detail Line Connection 2.0", assemblyPath, "Revit22_Plugin.Commands.RoofRidgeLineandPoints_01"));
            LineMenu.AddPushButton(new PushButtonData("Btn_RoofRidgeLineandPointsV1", "Detail Line Connection 2.0", assemblyPath, "Revit22_Plugin.Commands.RoofRidgeLineandPointsV1"));            
            LineMenu.AddPushButton(new PushButtonData("Btn_RoofRidgeLinepoints_03", "Perpendicular & Points 3.0", assemblyPath, "Revit22_Plugin.RRLPV3.Commands.RoofRidgeCommand_03"));
            LineMenu.AddPushButton(new PushButtonData("Btn_RoofRidgeLineandPoints_04", "Roof Ridge Lineand Points_V1", assemblyPath, "Revit22_Plugin.Commands.RoofRidgeLineandPointsV1"));

            PulldownButton ShapepointMenu = panel.AddItem(new PulldownButtonData("ShapepointMenu", "Shape Points")) as PulldownButton;
            //ShapepointMenu.LargeImage = IconManager.ToBitmapSource(Properties.Resources.addpoints32);
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_AddShapePointsFromLines", "Add Shape Points From Lines On roof Edge 1.0", assemblyPath, "Revit22_Plugin.Commands.AddRidgeAllPointsCommand_01"));//working-need some updation notify to select roof and multiple lines
            ShapepointMenu.AddPushButton(new PushButtonData("Btn_RoofLoopAnalyzerCommand_02", "Roof Inner Loop Divider 2.0", assemblyPath, "Revit22_Plugin.PDC.Commands.RoofLoopAnalyzerCommand_02"));//Working



            PulldownButton tagMenu = panel.AddItem(new PulldownButtonData("RoofTagMenu", "Tag")) as PulldownButton;
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
