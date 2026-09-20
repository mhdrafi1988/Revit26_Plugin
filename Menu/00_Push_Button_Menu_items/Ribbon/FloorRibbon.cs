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

            // Two approaches to the same job, collected under one pulldown
            // button per request — the higher-version build (V011) keeps the
            // icon, the Plan-View variant (V004) sits beside it in the dropdown.
            var fromRoomsV011 = new PushButtonData("Btn_FloorsAndRoofFromLinkedRooms_V011", "From Rooms V011", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V011.Command")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.FloorTools.FloorRoofFromRooms_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Floors And Roof From Linked Rooms", "V011")
            };
            var fromRoomsPlanViewV004 = new PushButtonData("Btn_FloorsAndRoofFromLinkedRoomsViaPlanViewV004", "Rooms Plan View V004", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRoomsViaPlanView.V004.Command")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.FloorTools.FloorRoofFromRoomsPlanView_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Floors And Roof From Linked Rooms (Via Plan View)", "V004")
            };
            var fromRoomsPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_FloorsAndRoofFromLinkedRooms", "From Rooms", fromRoomsV011);

            var items = RibbonLayoutHelper.AddStackedButtons(panel, new List<RibbonItemData> { fromRoomsPulldownData });
            RibbonLayoutHelper.WirePulldownButton(items, "Pulldown_FloorsAndRoofFromLinkedRooms", fromRoomsV011, fromRoomsPlanViewV004);
        }
    }
}
