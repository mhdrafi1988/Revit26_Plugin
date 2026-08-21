// =======================================================
// File: RoofDetailLineIntersectPayload.cs
// Location: Core/Models/
// New in V011. Carries the request across the ExternalEvent boundary.
// V008 held Document/FootPrintRoof/DetailLines directly on a private
// inner IExternalEventHandler class instead of a dedicated payload —
// functionally fine (the window never re-opens without them), but this
// matches the Payload/Result/Engine convention used by every other tool
// in the suite.
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.RoofDetailLineIntersect.V011.Core.Models
{
    public class RoofDetailLineIntersectPayload
    {
        public ElementId RoofId { get; set; }
        public List<ElementId> DetailLineIds { get; set; }

        public Action<LogEntry> Log { get; set; }
        public Action<RoofDetailLineIntersectResult> OnCompleted { get; set; }
    }
}
