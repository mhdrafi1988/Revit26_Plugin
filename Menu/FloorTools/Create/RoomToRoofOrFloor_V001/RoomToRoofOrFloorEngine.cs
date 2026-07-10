using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Revit26_Plugin.RoomToRoofOrFloor.V001.Core.Models;
using Revit26_Plugin.RoomToRoofOrFloor.V001.Core.Services;
using Revit26_Plugin.RoomToRoofOrFloor.V001.Infrastructure.Helpers;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.RoomToRoofOrFloor.V001.Core.Engine
{
    /// <summary>
    /// Per-room orchestration:
    ///   1. Get boundary, repair ONCE, cache the repaired CurveLoop(s).
    ///   2. TransactionGroup wraps both attempts so a partially-committed
    ///      roof attempt never leaves stray geometry if it's rolled back.
    ///   3. Attempt roof (user-chosen type) using the cached loop.
    ///   4. On failure (exception OR TransactionStatus != Committed),
    ///      attempt floor using the SAME cached loop + first available
    ///      floor type.
    ///   5. Both fail -> roll back group, log Error, skip room.
    /// </summary>
    public class RoomToRoofOrFloorEngine
    {
        private readonly Document _doc;
        private readonly RoomBoundaryService _boundaryService = new();
        private readonly LoopRepairService _repairService = new();

        public RoomToRoofOrFloorEngine(Document doc)
        {
            _doc = doc;
        }

        public RoomProcessingResult ProcessRoom(Room room, ElementId roofTypeId, Action<LogEntry> log)
        {
            var roomName = room.Name;

            var rawLoops = _boundaryService.GetBoundaryLoops(room);
            var repair = _repairService.Repair(rawLoops);

            if (!repair.IsRepairable)
            {
                log(new LogEntry(LogLevel.Warning,
                    $"{roomName} — loop unrepairable ({repair.FailureReason}), skipped"));
                return new RoomProcessingResult(room.Id, roomName,
                    RoomOutcome.SkippedUnrepairableLoop, repair.FailureReason, null);
            }

            var cachedLoops = repair.RepairedLoops; // same object reused for both attempts

            using var group = new TransactionGroup(_doc, $"RoomToRoofOrFloor - {roomName}");
            group.Start();

            bool roofOk = TryCreateRoof(cachedLoops, roofTypeId, out string roofFailReason);
            if (roofOk)
            {
                group.Assimilate();
                var note = string.IsNullOrEmpty(repair.Notes) ? "" : $" ({repair.Notes})";
                log(new LogEntry(LogLevel.Success, $"{roomName} — roof created{note}"));
                return new RoomProcessingResult(room.Id, roomName,
                    RoomOutcome.RoofCreated, null, repair.Notes);
            }

            bool floorOk = TryCreateFloor(cachedLoops, out string floorFailReason);
            if (floorOk)
            {
                group.Assimilate();
                log(new LogEntry(LogLevel.Warning,
                    $"{roomName} — roof failed ({roofFailReason}), floor created (fallback)"));
                return new RoomProcessingResult(room.Id, roomName,
                    RoomOutcome.FloorCreatedFallback, roofFailReason, repair.Notes);
            }

            group.RollBack();
            var combinedReason = $"roof: {roofFailReason}; floor: {floorFailReason}";
            log(new LogEntry(LogLevel.Error,
                $"{roomName} — both roof and floor failed, skipped ({combinedReason})"));
            return new RoomProcessingResult(room.Id, roomName,
                RoomOutcome.SkippedBothFailed, combinedReason, repair.Notes);
        }

        private bool TryCreateRoof(IList<CurveLoop> loops, ElementId roofTypeId, out string failReason)
        {
            using var sub = new SubTransaction(_doc);
            sub.Start();
            try
            {
                var level = GetLowestLevel();
                var curveArray = ToCurveArray(loops[0]); // outer loop is the footprint profile
                var roofType = _doc.GetElement(roofTypeId) as RoofType;

                _doc.Create.NewFootPrintRoof(curveArray, level, roofType, out ModelCurveArray _);

                var status = sub.Commit();
                if (status != TransactionStatus.Committed)
                {
                    failReason = $"TransactionStatus={status}";
                    return false;
                }

                failReason = null;
                return true;
            }
            catch (Exception ex)
            {
                if (sub.HasStarted() && !sub.HasEnded()) sub.RollBack();
                failReason = ex.Message;
                return false;
            }
        }

        private bool TryCreateFloor(IList<CurveLoop> loops, out string failReason)
        {
            using var sub = new SubTransaction(_doc);
            sub.Start();
            try
            {
                var floorType = RevitTypeHelper.GetFirstAvailableFloorType(_doc);
                if (floorType == null)
                {
                    sub.RollBack();
                    failReason = "No floor type available in document";
                    return false;
                }

                var level = GetLowestLevel();
                Floor.Create(_doc, loops, floorType.Id, level.Id);

                var status = sub.Commit();
                if (status != TransactionStatus.Committed)
                {
                    failReason = $"TransactionStatus={status}";
                    return false;
                }

                failReason = null;
                return true;
            }
            catch (Exception ex)
            {
                if (sub.HasStarted() && !sub.HasEnded()) sub.RollBack();
                failReason = ex.Message;
                return false;
            }
        }

        private CurveArray ToCurveArray(CurveLoop loop)
        {
            var arr = new CurveArray();
            foreach (var c in loop) arr.Append(c);
            return arr;
        }

        private Level GetLowestLevel()
        {
            return new FilteredElementCollector(_doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(l => l.Elevation)
                .First();
        }
    }
}
