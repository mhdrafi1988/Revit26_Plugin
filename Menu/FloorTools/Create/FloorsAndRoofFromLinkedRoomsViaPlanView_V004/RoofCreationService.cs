using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRoomsViaPlanView.V004
{
    /// <summary>
    /// ASSUMPTION (flagged): uses the classic instance API
    /// doc.Create.NewFootPrintRoof(CurveArray, Level, RoofType, out ModelCurveArray), which
    /// takes a flat CurveArray rather than CurveLoop/IList&lt;CurveLoop&gt;. Confirm this
    /// overload still exists in the Revit 2026 SDK — some newer API surfaces moved to
    /// static Create methods the way Floor.Create did.
    ///
    /// LIMITATION (flagged, not silently applied): only the OUTER boundary loop is used.
    /// NewFootPrintRoof does not accept inner loops the way Floor.Create does, so any
    /// inner loops (openings) present in a room's boundary are always skipped for roofs —
    /// this is different from floors, where inner loops are attempted and only skipped on
    /// individual validation failure. If you need roof openings, that's a separate feature
    /// (e.g. post-create opening cut) — not implemented here.
    /// </summary>
    public static class RoofCreationService
    {
        public static FootPrintRoof Create(Document doc, CurveLoop outerLoop, ElementId roofTypeId, Level level)
        {
            var footprint = new CurveArray();
            foreach (var curve in outerLoop)
                footprint.Append(curve);

            var roofType = doc.GetElement(roofTypeId) as RoofType;
            return doc.Create.NewFootPrintRoof(footprint, level, roofType, out _);
        }
    }
}
