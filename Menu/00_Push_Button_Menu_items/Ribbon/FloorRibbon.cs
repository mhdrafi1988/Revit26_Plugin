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

            Create.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint_05", "RoomToRoofOrFloor.V001", assemblyPath, "Revit26_Plugin.RoomToRoofOrFloor.V001.Commands.RoomToRoofOrFloor"));
            Create.AddPushButton(new PushButtonData("Btn_AutoSlopeByPoint_07", "FloorsFromLinkedRoomsViaPlanView.V001", assemblyPath, "Revit26_Plugin.FloorsFromLinkedRoomsViaPlanView.V001.Command"));
            Create.AddPushButton(new PushButtonData("Btn_FloorsAndRoofFromLinkedRoomsViaPlanViewV003", "FloorsAndRoofFromLinkedRoomsViaPlanView.V003", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRoomsViaPlanView.V003.Command"));
            Create.AddPushButton(new PushButtonData("Btn_RoomToRoofff ", "RoomToRoof", assemblyPath, "Revit26_Plugin.RoomToRoof.RoomToRoofCommand"));

           








        }
    }
}