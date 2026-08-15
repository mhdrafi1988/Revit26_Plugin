using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V011
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

            // KNOWN LIMITATION (carried from V004, flagged): grouping is by document
            // Title, so two *different* link files that happen to share a title would
            // merge into one option. Accepted as an edge case for now.
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
        /// All placed rooms in the link, across every level in that linked document.
        /// Level per room is read from the room's own Level property (Room.Level.Name).
        /// V005: the LinkInstanceOption parameter was dropped — the instance transform
        /// is applied once per run from CreateRunRequest.LinkTransform, not stored per room.
        /// </summary>
        public static List<RoomCandidate> GetAllRooms(Document linkDoc)
        {
            var results = new List<RoomCandidate>();

            // Area unit + symbol taken from the linked doc's own project units, so the
            // displayed number always matches what that model's team is used to seeing.
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

                // Rooms with a null Level (rare — corrupt/orphaned elements) are still
                // surfaced with a placeholder level name so they aren't silently dropped;
                // they won't auto-match any host level and stay "unmapped" until the user
                // picks one manually.
                string linkedLevelName = room.Level?.Name ?? "(no level)";

                // Raw elevation of the room's linked Level, in the LINKED document's
                // own coordinate space (internal units/feet). Null when the room has no
                // Level (see above) — LevelMatchingService treats null as unmatchable.
                // The link placement transform is applied later, at match time, not here —
                // this stays a pure read of the linked document.
                double? linkedLevelElevation = room.Level?.Elevation;

                results.Add(new RoomCandidate
                {
                    SerialNumber = serial++,
                    RoomId = room.Id,
                    RoomElement = room,
                    DisplayName = $"{room.Number} - {room.Name}",
                    RoomName = room.Name,
                    RoomNumber = room.Number,
                    LinkedLevelName = linkedLevelName,
                    LinkedLevelElevation = linkedLevelElevation,
                    AreaDisplay = areaDisplay,
                    AreaUnitSymbol = areaUnitSymbol
                });
            }

            return results;
        }
    }
}
