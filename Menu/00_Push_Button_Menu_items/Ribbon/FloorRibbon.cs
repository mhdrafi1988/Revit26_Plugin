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

            RibbonLayoutHelper.AddStackedButtons(panel, new List<RibbonItemData>
            {
                new PushButtonData("Btn_FloorsAndRoofFromLinkedRoomsViaPlanViewV004", "Rooms (Plan View)", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRoomsViaPlanView.V004.Command")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.FloorTools.FloorRoofFromRoomsPlanView_16.png"),
                    ToolTip = "Floors And Roof From Linked Rooms (Via Plan View)"
                },
                new PushButtonData("Btn_FloorsAndRoofFromLinkedRooms_V011", "From Rooms", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V011.Command")
                {
                    Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.FloorTools.FloorRoofFromRooms_16.png"),
                    ToolTip = "Floors And Roof From Linked Rooms"
                },
            });
        }
    }
}
