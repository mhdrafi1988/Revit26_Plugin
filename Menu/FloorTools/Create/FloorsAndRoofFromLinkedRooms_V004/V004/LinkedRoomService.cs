using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V004
{
    public static class LinkedRoomService
    {
        public static List<LinkedDocumentOption> GetLinkedDocumentsWithRooms(Document hostDoc)
        {
            var instances = new FilteredElementCollector(hostDoc)
                .OfClass(typeof(RevitLinkInstance))
                .Cast<RevitLinkInstance>()
                .Where(i => i.GetLinkDocument() != null)
                .ToList();

            var byDoc = instances.GroupBy(i => i.GetLinkDocument().Title);

            var result = new List<LinkedDocumentOption>();
            foreach (var group in byDoc)
            {
                var linkDoc = group.First().GetLinkDocument();

                bool hasRooms = new FilteredElementCollector(linkDoc)
                    .OfCategory(BuiltInCategory.OST_Rooms)
                    .WhereElementIsNotElementType()
                    .Any();

                if (!hasRooms) continue;

                var option = new LinkedDocumentOption
                {
                    DisplayName = group.Key,
                    LinkDocument = linkDoc
                };

                int idx = 1;
                foreach (var inst in group)
                {
                    option.Instances.Add(new LinkInstanceOption
                    {
                        DisplayName = $"{group.Key} (instance {idx})",
                        InstanceId = inst.Id,
                        Transform = inst.GetTotalTransform()
                    });
                    idx++;
                }

                result.Add(option);
            }

            return result;
        }

        /// <summary>
        /// All placed rooms in the link, across every level in that linked document — the
        /// active-view-level restriction from V003 has been dropped per spec. Level per room
        /// is now read from the room's own Level property (Room.Level.Name) rather than a
        /// bounding-box/Z-elevation heuristic against one target level; this is a behavior
        /// change from V003, confirmed in-conversation, since we need a real per-room level
        /// name for the "Linked File Level" column rather than a yes/no test against one level.
        /// </summary>
        public static List<RoomCandidate> GetAllRooms(Document linkDoc, LinkInstanceOption instanceOption)
        {
            var results = new List<RoomCandidate>();

            // Area unit + symbol taken from the linked doc's own project units, so the
            // displayed number always matches what that model's team is used to seeing —
            // no hardcoded metric/imperial assumption.
            var areaUnitTypeId = linkDoc.GetUnits().GetFormatOptions(SpecTypeId.Area).GetUnitTypeId();
            string areaUnitSymbol = LabelUtils.GetLabelForUnit(areaUnitTypeId);

            var rooms = new FilteredElementCollector(linkDoc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Room>()
                .Where(r => r.Area > 0) // skip unplaced rooms
                .OrderBy(r => r.Level?.Elevation ?? double.MaxValue)
                .ThenBy(r => r.Number);

            int serial = 1;
            foreach (var room in rooms)
            {
                double areaDisplay = UnitUtils.ConvertFromInternalUnits(room.Area, areaUnitTypeId);

                // ASSUMPTION (flagged): rooms with a null Level (rare — can happen for
                // corrupt/orphaned room elements) are still surfaced with a placeholder
                // level name so they aren't silently dropped from the grid; they simply
                // won't auto-match any host level and stay "unmapped" until the user
                // picks one manually.
                string linkedLevelName = room.Level?.Name ?? "(no level)";

                results.Add(new RoomCandidate
                {
                    SerialNumber = serial++,
                    RoomId = room.Id,
                    RoomElement = room,
                    Transform = instanceOption.Transform,
                    DisplayName = $"{room.Number} - {room.Name}",
                    RoomName = room.Name,
                    RoomNumber = room.Number,
                    LinkedLevelName = linkedLevelName,
                    AreaDisplay = areaDisplay,
                    AreaUnitSymbol = areaUnitSymbol
                });
            }

            return results;
        }
    }
}
