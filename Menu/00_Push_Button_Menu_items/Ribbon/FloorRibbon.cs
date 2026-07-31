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
            Create.AddPushButton(new PushButtonData("Btn_FloorsAndRoofFromLinkedRooms.V004", "FloorsAndRoofFromLinkedRooms.V004", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V004.Command"));
            Create.AddPushButton(new PushButtonData("Btn_FloorsAndRoofFromLinkedRooms.V005", "FloorsAndRoofFromLinkedRooms.V005", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V005.Command"));
            Create.AddPushButton(new PushButtonData("Btn_FloorsAndRoofFromLinkedRooms.V006", "FloorsAndRoofFromLinkedRooms.V006", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V006.Command"));
            Create.AddPushButton(new PushButtonData("Btn_RoofCreateTest.V001", "RoofCreateTest.V001", assemblyPath, "Revit26_Plugin.RoofCreateTest.V001.RoofCreateTestCommand"));
            Create.AddPushButton(new PushButtonData("Btn_V001", "RoofCreationIsolationTest.V001", assemblyPath, "Revit26_Plugin.RoofCreationIsolationTest.V001.Commands.RunTestCommand"));

            














        }
    }
}