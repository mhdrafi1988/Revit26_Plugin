using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
using System;
using System.Reflection;
using System.Windows.Markup;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class FloorToolsRibbon
    {

        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = app.CreateRibbonPanel(tabName, "Floor Tools");

            PulldownButton Create = panel.AddItem(new PulldownButtonData("FloorCreateMenu", "Floor Create 2")) as PulldownButton;
            Create.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.Autoslope32.png");

            //Floor From Room


            Create.AddPushButton(new PushButtonData("Btn_FloorsAndRoofFromLinkedRoomsViaPlanViewV003", "FloorsAndRoofFromLinkedRoomsViaPlanView.V003", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRoomsViaPlanView.V003.Command"));
            Create.AddPushButton(new PushButtonData("Btn_FloorsAndRoofFromLinkedRoomsViaPlanViewV004", "FloorsAndRoofFromLinkedRoomsViaPlanView.V004", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRoomsViaPlanView.V004.Command"));
            Create.AddPushButton(new PushButtonData("Btn_FloorsAndRoofFromLinkedRooms.V004", "FloorsAndRoofFromLinkedRooms.V004", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V004.Command"));
            Create.AddPushButton(new PushButtonData("Btn_FloorsAndRoofFromLinkedRooms.V005", "FloorsAndRoofFromLinkedRooms.V005", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V005.Command"));
            Create.AddPushButton(new PushButtonData("Btn_FloorsAndRoofFromLinkedRooms.V006", "FloorsAndRoofFromLinkedRooms.V006", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V006.Command"));
            Create.AddPushButton(new PushButtonData("Btn_FloorsAndRoofFromLinkedRooms.V007", "FloorsAndRoofFromLinkedRooms.V007", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V007.Command"));
            Create.AddPushButton(new PushButtonData("Btn_FloorsAndRoofFromLinkedRooms.V008", "FloorsAndRoofFromLinkedRooms.V008 #", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V008.Command"));


            Create.AddPushButton(new PushButtonData("Btn_RoofCreateTest.V001", "RoofCreateTest.V001", assemblyPath, "Revit26_Plugin.RoofCreateTest.V001.RoofCreateTestCommand"));
            Create.AddPushButton(new PushButtonData("Btn_V001", "RoofCreationIsolationTest.V001", assemblyPath, "Revit26_Plugin.RoofCreationIsolationTest.V001.Commands.RunTestCommand"));
            Create.AddPushButton(new PushButtonData("Btn_V002", "RoofCreationIsolationTest.V002", assemblyPath, "Revit26_Plugin.RoofCreationIsolationTest.V002.Commands.RunTestCommand"));
            Create.AddPushButton(new PushButtonData("Btn_FloorsAndRoofFromLinkedRooms_V011", "Floors And Roof From Linked Rooms V011", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V011.Command"));
            Create.AddPushButton(new PushButtonData("Btn_FloorsFromLinkedRoomsViaPlanView_V002", "Floors From Linked Rooms Via Plan View V002", assemblyPath, "Revit26_Plugin.FloorsFromLinkedRoomsViaPlanView.V002.Command"));
            Create.AddPushButton(new PushButtonData("Btn_RoomToRoofOrFloor_V002", "Room To Roof Or Floor V002", assemblyPath, "Revit26_Plugin.RoomToRoofOrFloor.V002.Commands.RoomToRoofOrFloor"));

            














        }
    }
}