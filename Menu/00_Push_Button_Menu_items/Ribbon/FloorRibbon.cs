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
            // button per request — each approach lists its previous version
            // first and its UI Standard version second; V011 keeps the icon.
            var fromRoomsV011 = new PushButtonData("Btn_FloorsAndRoofFromLinkedRooms_V011", "From Rooms V011", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V011.Command")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.FloorTools.FloorRoofFromRooms_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Floors And Roof From Linked Rooms", "V011")
            };
            var fromRoomsV012 = new PushButtonData("Btn_FloorsAndRoofFromLinkedRooms_V012", "From Rooms V012", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V012.Command")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.FloorTools.FloorRoofFromRooms_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Floors And Roof From Linked Rooms", "V012",
                    "UI Standard layout: footer with progress, summaries, always-visible log and Create Floors / Create Roof → Close.")
            };
            var fromRoomsPlanViewV005 = new PushButtonData("Btn_FloorsAndRoofFromLinkedRoomsViaPlanViewV005", "Rooms Plan View V005", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRoomsViaPlanView.V005.Command")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.FloorTools.FloorRoofFromRoomsPlanView_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Floors And Roof From Linked Rooms (Via Plan View)", "V005",
                    "UI Standard layout: metrics card, full-height room list, footer log and Create Floors / Create Roof → Close.")
            };
            var fromRoomsPlanViewV006 = new PushButtonData("Btn_FloorsAndRoofFromLinkedRoomsViaPlanViewV006", "Rooms Plan View V006", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRoomsViaPlanView.V006.Command")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.FloorTools.FloorRoofFromRoomsPlanView_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Floors And Roof From Linked Rooms (Via Plan View)", "V006",
                    "V005 on the shared ToolWindowShell (summaries and log in body, capped room list).")
            };
            var fromRoomsPulldownData = RibbonLayoutHelper.CreatePulldownButtonData("Pulldown_FloorsAndRoofFromLinkedRooms", "From Rooms", fromRoomsV011);

            var items = RibbonLayoutHelper.AddStackedButtons(panel, new List<RibbonItemData> { fromRoomsPulldownData });
            RibbonLayoutHelper.WirePulldownButton(items, "Pulldown_FloorsAndRoofFromLinkedRooms", fromRoomsV011, fromRoomsV012, fromRoomsPlanViewV005, fromRoomsPlanViewV006);
        }
    }
}
