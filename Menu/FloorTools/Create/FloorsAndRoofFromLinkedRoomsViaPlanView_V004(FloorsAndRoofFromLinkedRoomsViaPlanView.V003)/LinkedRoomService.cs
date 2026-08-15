using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRoomsViaPlanView.V004
{
    /// <summary>
    /// ASSUMPTION (flagged, not silently applied): elevation overlap is checked using only
    /// the Z component of the link instance's transform (transform.Origin.Z). Rotated or
    /// tilted link placements are not accounted for. Flag if any links are rotated about
    /// X/Y in your models — the overlap check below would need the full transform applied
    /// to each boundary curve, which RoomBoundaryService already does for the geometry itself.
    /// </summary>
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

        /// <summary>Elevation-overlap fudge factor — small tolerance so a room whose base or
        /// top lands exactly on the level plane still counts as intersecting it.</summary>
        private const double ElevationToleranceFt = 0.01;

        /// <summary>
        /// Rooms in the link whose vertical extent contains the active level's elevation.
        /// Height is read from the room's actual bounding box (real geometry) rather than a
        /// parameter, so it works for both bounded and "unbounded" rooms without guessing.
        /// </summary>
        public static List<RoomCandidate> GetRoomsAtLevel(Document linkDoc, LinkInstanceOption instanceOption, Level activeLevel)
        {
            var results = new List<RoomCandidate>();
            double targetElev = activeLevel.Elevation;
            double zShift = instanceOption.Transform.Origin.Z;

            // Area unit + symbol taken from the linked doc's own project units, so the
            // displayed number always matches what that model's team is used to seeing —
            // no hardcoded metric/imperial assumption.
            var areaUnitTypeId = linkDoc.GetUnits().GetFormatOptions(SpecTypeId.Area).GetUnitTypeId();
            string areaUnitSymbol = LabelUtils.GetLabelForUnit(areaUnitTypeId);

            var rooms = new FilteredElementCollector(linkDoc)
                .OfCategory(BuiltInCategory.OST_Rooms)
                .WhereElementIsNotElementType()
                .Cast<Room>()
                .Where(r => r.Area > 0); // skip unplaced rooms

            foreach (var room in rooms)
            {
                var bbox = room.get_BoundingBox(null);
                if (bbox == null) continue; // truly unplaced/unbounded — nothing to test against

                double shiftedBase = bbox.Min.Z + zShift;
                double shiftedTop = bbox.Max.Z + zShift;

                if (targetElev >= shiftedBase - ElevationToleranceFt && targetElev <= shiftedTop + ElevationToleranceFt)
                {
                    double areaDisplay = UnitUtils.ConvertFromInternalUnits(room.Area, areaUnitTypeId);
                    results.Add(new RoomCandidate
                    {
                        RoomId = room.Id,
                        RoomElement = room,
                        Transform = instanceOption.Transform,
                        DisplayName = $"{room.Number} - {room.Name}",
                        AreaDisplay = areaDisplay,
                        AreaUnitSymbol = areaUnitSymbol
                    });
                }
            }

            return results;
        }
    }
}
