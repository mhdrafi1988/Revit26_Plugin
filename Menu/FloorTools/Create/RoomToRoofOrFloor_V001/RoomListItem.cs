using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.RoomToRoofOrFloor.V001.Core.Models
{
    /// <summary>
    /// Row model for the Rooms grid. IsSelected drives which rooms
    /// are included when Run executes.
    /// </summary>
    public partial class RoomListItem : ObservableObject
    {
        public ElementId RoomId { get; }
        public string RoomName { get; }
        public string RoomNumber { get; }
        public string LevelName { get; }

        [ObservableProperty]
        private bool _isSelected;

        public RoomListItem(ElementId roomId, string roomName, string roomNumber, string levelName, bool isSelected = true)
        {
            RoomId = roomId;
            RoomName = roomName;
            RoomNumber = roomNumber;
            LevelName = levelName;
            _isSelected = isSelected;
        }
    }
}
