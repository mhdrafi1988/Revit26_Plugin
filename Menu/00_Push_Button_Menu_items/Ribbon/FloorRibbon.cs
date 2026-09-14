using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;
using System.Collections.Generic;

namespace Revit26_Plugin.Menu.Ribbon
{
    public static class FloorToolsRibbon
    {
        public static void Build(UIControlledApplication app, string tabName, string assemblyPath)
        {
            RibbonPanel panel = app.CreateRibbonPanel(tabName, "Floor Tools");

            RibbonLayoutHelper.AddStackedButtons(panel, new List<PushButtonData>
            {
                new PushButtonData("Btn_FloorsAndRoofFromLinkedRoomsViaPlanViewV004", "Floors And Roof From Linked Rooms (Via Plan View)", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRoomsViaPlanView.V004.Command")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.FloorTools.FloorRoofFromRoomsPlanView_32.png")
                },
                new PushButtonData("Btn_FloorsAndRoofFromLinkedRooms_V011", "Floors And Roof From Linked Rooms", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V011.Command")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.FloorTools.FloorRoofFromRooms_32.png")
                },
            });
        }
    }
}
