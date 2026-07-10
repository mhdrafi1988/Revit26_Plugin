using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin.RoomToRoofOrFloor.V001.Core.Models;

namespace Revit26_Plugin.RoomToRoofOrFloor.V001.Infrastructure.Helpers
{
    /// <summary>
    /// Type lookups. Roof type is user-chosen (dropdown). Floor fallback
    /// type is automatic: first available floor type, ordered by
    /// ElementId ascending — deterministic across reruns.
    /// </summary>
    public static class RevitTypeHelper
    {
        public static List<RoofTypeOption> GetRoofTypes(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .Cast<RoofType>()
                .OrderBy(t => t.Id.Value)
                .Select(t => new RoofTypeOption(t.Id, t.Name))
                .ToList();
        }

        public static FloorType GetFirstAvailableFloorType(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>()
                .OrderBy(t => t.Id.Value)
                .FirstOrDefault();
        }
    }
}
