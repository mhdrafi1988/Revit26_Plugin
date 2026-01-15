using Autodesk.Revit.DB;
using System.Collections.Generic;

namespace Revit26_Plugin.CreaserAdv_V00.Services.DetailItems
{
    /// <summary>
    /// Immutable result object describing a detail item placement operation.
    /// DTO only – contains NO Revit API logic.
    /// </summary>
    public class DetailItemPlacementResult
    {
        public int AttemptedCount { get; }
        public int PlacedCount { get; }
        public IReadOnlyList<ElementId> CreatedElementIds { get; }

        public bool IsSuccess => PlacedCount > 0;

        public DetailItemPlacementResult(
            int attemptedCount,
            int placedCount,
            IList<ElementId> createdElementIds)
        {
            AttemptedCount = attemptedCount;
            PlacedCount = placedCount;
            CreatedElementIds = new List<ElementId>(createdElementIds);
        }
    }
}
