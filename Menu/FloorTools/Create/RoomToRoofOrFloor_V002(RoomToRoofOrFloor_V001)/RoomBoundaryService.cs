using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace Revit26_Plugin.RoomToRoofOrFloor.V002.Core.Services
{
    /// <summary>
    /// Extracts a room's raw boundary as CurveLoops (one per loop Revit
    /// returns — usually 1, more when the room has interior islands).
    /// Output feeds directly into LoopRepairService.
    /// </summary>
    public class RoomBoundaryService
    {
        public IList<CurveLoop> GetBoundaryLoops(Room room)
        {
            var loops = new List<CurveLoop>();

            var options = new SpatialElementBoundaryOptions
            {
                SpatialElementBoundaryLocation = SpatialElementBoundaryLocation.Finish
            };

            var segmentLoops = room.GetBoundarySegments(options);
            if (segmentLoops == null) return loops;

            foreach (var segmentLoop in segmentLoops)
            {
                var loop = new CurveLoop();
                foreach (var segment in segmentLoop)
                {
                    var curve = segment.GetCurve();
                    if (curve != null)
                        loop.Append(curve);
                }
                if (loop.NumberOfCurves() > 0)
                    loops.Add(loop);
            }

            return loops;
        }
    }
}
