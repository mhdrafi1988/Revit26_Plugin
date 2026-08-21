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
            Create.LargeImage = ImageUtils.Load("Revit26_Plugin.Resources.Icons.FloorTools.FloorCreate_32.png");

            //Floor From Room
            Create.AddPushButton(new PushButtonData("Btn_FloorsAndRoofFromLinkedRoomsViaPlanViewV004", "FloorsAndRoofFromLinkedRoomsViaPlanView.V004", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRoomsViaPlanView.V004.Command"));
            Create.AddPushButton(new PushButtonData("Btn_FloorsAndRoofFromLinkedRooms_V011", "Floors And Roof From Linked Rooms V011", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V011.Command"));
                        
        }
    }
}