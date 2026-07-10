namespace Revit26_Plugin.RoomToRoofOrFloor.V001.Core.Models
{
    /// <summary>
    /// Per-room result of the roof-then-floor-fallback attempt.
    /// </summary>
    public enum RoomOutcome
    {
        RoofCreated,
        FloorCreatedFallback,
        SkippedUnrepairableLoop,
        SkippedBothFailed
    }
}
