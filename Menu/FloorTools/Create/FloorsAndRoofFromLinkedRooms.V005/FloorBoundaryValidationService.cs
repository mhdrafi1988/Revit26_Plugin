using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V005
{
    /// <summary>
    /// Validates a room's full loop set (outer + inner) before it is passed to
    /// Floor.Create(Document, IList&lt;CurveLoop&gt;, ElementId, ElementId). Floors keep
    /// all inner loops (holes), unlike roofs — so InnerLoopsDropped is always 0 here.
    ///
    /// Uses BoundaryValidation.IsValidHorizontalBoundary(), the official Revit API
    /// validator for Floor/Ceiling horizontal boundaries (Revit 2022+, confirmed present
    /// in the 2026 API). This is a supplementary check on top of RoomBoundaryService's
    /// own planarity/self-intersection checks — it catches cases the API itself would
    /// reject that our custom checks may not (e.g. loop-to-loop relationships between
    /// outer and inner loops).
    /// </summary>
    public static class FloorBoundaryValidationService
    {
        public static ValidationResult Validate(IList<CurveLoop> loops)
        {
            var result = new ValidationResult { InnerLoopsDropped = 0, InnerLoopsWarning = null };

            if (loops == null || loops.Count == 0)
            {
                result.IsValid = false;
                result.FailureReason = "No boundary loops available for floor creation.";
                return result;
            }

            foreach (var loop in loops)
            {
                if (loop == null || loop.IsOpen())
                {
                    result.IsValid = false;
                    result.FailureReason = "One or more boundary loops are open (not closed) — cannot create floor.";
                    return result;
                }
            }

            bool isValidBoundary;
            try
            {
                isValidBoundary = BoundaryValidation.IsValidHorizontalBoundary(loops);
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
                result.FailureReason = "BoundaryValidation.IsValidHorizontalBoundary() rejected the loop set " +
                                        "(overlapping/invalid loop relationships for a floor profile).";
                return result;
            }

            result.IsValid = true;
            return result;
        }
    }
}
