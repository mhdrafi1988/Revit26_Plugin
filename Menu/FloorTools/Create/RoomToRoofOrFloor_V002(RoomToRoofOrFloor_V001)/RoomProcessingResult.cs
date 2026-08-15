using Autodesk.Revit.DB;

namespace Revit26_Plugin.RoomToRoofOrFloor.V002.Core.Models
{
    /// <summary>
    /// Outcome of processing a single room: which path was taken
    /// (roof / floor fallback / skipped) and why.
    /// </summary>
    public class RoomProcessingResult
    {
        public ElementId RoomId { get; }
        public string RoomName { get; }
        public RoomOutcome Outcome { get; }
        public string Reason { get; }
        public string RepairNotes { get; }

        public RoomProcessingResult(ElementId roomId, string roomName, RoomOutcome outcome,
            string reason, string repairNotes)
        {
            RoomId = roomId;
            RoomName = roomName;
            Outcome = outcome;
            Reason = reason;
            RepairNotes = repairNotes;
        }
    }
}
