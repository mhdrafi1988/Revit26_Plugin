using Autodesk.Revit.UI;
using Revit26_Plugin.Resources.Icons;

namespace Revit26_Plugin.Menu.Ribbon
{
    /// <summary>
    /// Builds the Floors And Roof From Linked Rooms pulldown. Since plugin 2.4.0 it sits in the
    /// Roof Tools panel (see <see cref="RoofToolsRibbon"/>); the old one-button Floor Tools panel
    /// was removed to keep the tab narrow enough for Revit to show every button name.
    /// </summary>
    public static class FloorToolsRibbon
    {
        /// <summary>Internal name of the pulldown (unchanged since it lived in Floor Tools).</summary>
        public const string FromRoomsPulldownName = "Pulldown_FloorsAndRoofFromLinkedRooms";

        /// <summary>
        /// Returns the "Floor From Rooms" pulldown data to stack in a panel, and in
        /// <paramref name="versions"/> every version, highest first, for
        /// <see cref="RibbonLayoutHelper.WirePulldownButton"/>.
        /// </summary>
        public static PulldownButtonData CreateFromRoomsPulldown(string assemblyPath, out PushButtonData[] versions)
        {
            // Two approaches to the same job, collected under one pulldown
            // button per request — each approach lists its highest version
            // number first, every older version below it; V013 keeps the icon.
            var fromRoomsV012 = new PushButtonData("Btn_FloorsAndRoofFromLinkedRooms_V012", "From Rooms V012", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V012.Command")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.FloorTools.FloorRoofFromRooms_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Floors And Roof From Linked Rooms", "V012",
                    "UI Standard layout: footer with progress, summaries, always-visible log and Create Floors / Create Roof → Close.")
            };
            var fromRoomsV013 = new PushButtonData("Btn_FloorsAndRoofFromLinkedRooms_V013", "From Rooms V013", assemblyPath, "Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V013.Command")
            {
                Image = ImageUtils.Load("Revit26_Plugin.Resources.Icons.FloorTools.FloorRoofFromRooms_16.png"),
                ToolTip = RibbonLayoutHelper.VersionTip("Floors And Roof From Linked Rooms", "V013",
                    "V012 on the shared ToolWindowShell (summaries and log in body, capped room grid).")
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
            versions = new[] { fromRoomsV013, fromRoomsV012, fromRoomsPlanViewV006, fromRoomsPlanViewV005 };
            return RibbonLayoutHelper.CreatePulldownButtonData(FromRoomsPulldownName, "Floor From Rooms", fromRoomsV013);
        }
    }
}
