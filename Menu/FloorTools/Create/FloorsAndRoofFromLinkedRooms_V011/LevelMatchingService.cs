using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V011
{
    /// <summary>
    /// Loads host-model levels for the "New Level" dropdown and auto-matches each linked
    /// room's level against them by ELEVATION (Matches by elevation rather than by level name
    /// match). No host level is ever created; a room with no elevation match within
    /// tolerance is left unmapped, per confirmed spec.
    /// </summary>
    public static class LevelMatchingService
    {
        /// <summary>All host Levels available as mapping targets, ordered by elevation.</summary>
        public static List<HostLevelOption> GetHostLevels(Document hostDoc)
        {
            return new FilteredElementCollector(hostDoc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .Select(l => new HostLevelOption { Name = l.Name, LevelElement = l })
                .ToList();
        }

        /// <summary>
        /// Elevation-based level matching. The linked room's level elevation (in the linked
        /// document's own coordinate space) is converted to host-space by adding the link
        /// instance's placement Z-offset (linkTransform.Origin.Z + linkedElevation *
        /// linkTransform.BasisZ.Z, to also account for any non-identity rotation of the
        /// vertical axis — in the overwhelmingly common case of a level link with no tilt,
        /// this reduces to linkedElevation + linkTransform.Origin.Z). The closest host level
        /// within toleranceFeet is returned; null (unmapped) if none qualifies.
        ///
        /// toleranceFeet: 0.0 = exact match only (checkbox checked in UI). Otherwise the
        /// UI-entered mm value converted to feet (see MainViewModel.ToleranceFeet).
        ///
        /// Returns null (leaves the room unmapped) when linkedLevelElevation is null (room
        /// had no Level in the link) or no host level is within tolerance — never falls
        /// back to a default level, per confirmed spec.
        /// </summary>
        public static HostLevelOption FindMatch(
            double? linkedLevelElevation,
            Transform linkTransform,
            IEnumerable<HostLevelOption> hostLevels,
            double toleranceFeet)
        {
            if (linkedLevelElevation == null) return null;

            double effectiveElevation = linkTransform.OfPoint(new XYZ(0, 0, linkedLevelElevation.Value)).Z;

            HostLevelOption best = null;
            double bestDiff = double.MaxValue;

            foreach (var host in hostLevels)
            {
                double diff = Math.Abs(host.LevelElement.Elevation - effectiveElevation);
                if (diff <= toleranceFeet && diff < bestDiff)
                {
                    best = host;
                    bestDiff = diff;
                }
            }

            return best;
        }
    }
}
