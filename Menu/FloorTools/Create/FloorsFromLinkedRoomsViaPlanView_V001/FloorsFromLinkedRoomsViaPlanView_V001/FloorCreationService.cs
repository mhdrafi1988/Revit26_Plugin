using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.FloorsFromLinkedRoomsViaPlanView.V001
{
    /// <summary>
    /// ASSUMPTION (flagged): uses the Revit 2022+ Floor.Create(document, profile, floorTypeId,
    /// levelId) overload, non-structural. Confirm this matches the Revit 2026 API signature
    /// in your installed SDK before compiling — floor creation overloads have shifted across
    /// versions.
    /// </summary>
    public static class FloorCreationService
    {
        public static Floor Create(Document doc, IList<CurveLoop> loops, ElementId floorTypeId, Level level)
        {
            return Floor.Create(doc, loops, floorTypeId, level.Id);
        }
    }
}
