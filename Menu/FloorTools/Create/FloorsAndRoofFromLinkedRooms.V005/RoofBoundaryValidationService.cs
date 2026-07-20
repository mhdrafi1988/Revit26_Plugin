using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V005
{
    /// <summary>
    /// Validates a room's boundary before it is passed to RoofCreationService /
    /// doc.Create.NewFootPrintRoof(). Unlike floors, footprint roofs only ever use the
    /// OUTER loop — NewFootPrintRoof's CurveArray parameter takes a single boundary,
    /// so any inner loops (courtyards, shafts) on the room are necessarily dropped.
    ///
    /// This service:
    ///  1. Validates only boundary.Loops[0] (the outer loop) for roof creation.
    ///  2. Enforces the stricter roof-sketch curve-type rule: Line/Arc only. Floor.Create
    ///     accepts ellipses and splines; footprint roof sketches are known to reject or
    ///     misbehave on them, so we fail fast here with a clear reason instead of letting
    ///     NewFootPrintRoof fail with a vague message.
    ///  3. Computes InnerLoopsDropped = (valid inner loops NewFootPrintRoof can't use) +
    ///     (inner loops that separately failed geometric validation upstream in
    ///     RoomBoundaryService) and builds the Warning text — the same formula
    ///     previously inlined in RunCreateElementsExternalEventHandler, now centralized
    ///     here so the handler doesn't compute it twice.
    /// </summary>
    public static class RoofBoundaryValidationService
    {
        public static ValidationResult Validate(BoundaryResult boundary)
        {
            var result = new ValidationResult();

            if (boundary == null || boundary.Loops == null || boundary.Loops.Count == 0)
            {
                result.IsValid = false;
                result.FailureReason = "No boundary loops available for roof creation.";
                return result;
            }

            var outerLoop = boundary.Loops[0];

            if (outerLoop == null || outerLoop.IsOpen())
            {
                result.IsValid = false;
                result.FailureReason = "Outer boundary loop is open (not closed) — cannot create roof.";
                return result;
            }

            // Roof-specific curve-type restriction: Line/Arc only.
            foreach (var curve in outerLoop)
            {
                if (!(curve is Line) && !(curve is Arc))
                {
                    result.IsValid = false;
                    result.FailureReason = $"Outer boundary contains an unsupported curve type " +
                                            $"({curve.GetType().Name}) for roof sketches — only lines and arcs are supported.";
                    return result;
                }
            }

            bool isValidBoundary;
            try
            {
                isValidBoundary = BoundaryValidation.IsValidHorizontalBoundary(new List<CurveLoop> { outerLoop });
            }
            catch (System.Exception ex)
            {
                result.IsValid = false;
                result.FailureReason = $"Boundary validation threw an exception: {ex.Message}";
                return result;
            }

            if (!isValidBoundary)
            {
                result.IsValid = false;
                result.FailureReason = "BoundaryValidation.IsValidHorizontalBoundary() rejected the outer loop " +
                                        "as a roof footprint.";
                return result;
            }

            result.IsValid = true;

            // Inner-loop drop accounting — same formula as V005's handler-inline calc:
            // valid inner loops (Loops.Count - 1) are unsupported by NewFootPrintRoof,
            // plus any inner loops RoomBoundaryService already discarded for failing
            // its own geometric checks (InnerLoopsSkipped).
            int dropped = (boundary.Loops.Count - 1) + boundary.InnerLoopsSkipped;
            result.InnerLoopsDropped = dropped;
            result.InnerLoopsWarning = dropped > 0
                ? $"{dropped} inner loop(s) not supported for roofs, outer boundary used"
                : null;

            return result;
        }
    }
}
