using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRooms.V005
{
    /// <summary>
    /// V005: the carried-over assumption from V004 is resolved — Floor.Create(Document,
    /// IList&lt;CurveLoop&gt;, ElementId floorTypeId, ElementId levelId) is confirmed against
    /// the Revit API docs as the correct non-structural overload, present through the
    /// 2026 API. The profile may contain lines, arcs, ellipses, and splines (floors are
    /// less restrictive than roof footprints — see RoofBoundaryValidationService).
    ///
    /// Geometric boundary validation (closed loops, BoundaryValidation.IsValidHorizontalBoundary)
    /// now happens upstream in FloorBoundaryValidationService, called by the handler
    /// before this method runs.
    /// </summary>
    public static class FloorCreationService
    {
        public static Floor Create(Document doc, IList<CurveLoop> loops, ElementId floorTypeId, Level level)
        {
            return Floor.Create(doc, loops, floorTypeId, level.Id);
        }
    }
}
