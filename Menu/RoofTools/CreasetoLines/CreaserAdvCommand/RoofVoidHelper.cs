using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit26_Plugin.Creaser_adv_V001.Helpers
{
    public static class RoofVoidHelper
    {
        public static IList<CurveLoop> GetRoofVoidLoops(FootPrintRoof roof)
        {
            var loops = new List<CurveLoop>();

            if (roof is not HostObject host)
                return loops;

            Document doc = roof.Document;

            // ✅ Use positional booleans (no named args)
            // Signature varies by version/names, but order is consistent:
            // (openings, shadows, embedded, shared)
            ICollection<ElementId> insertIds = host.FindInserts(true, false, false, false);

            foreach (ElementId id in insertIds)
            {
                if (doc.GetElement(id) is not Opening opening)
                    continue;

                foreach (CurveArray ca in opening.BoundaryCurves)
                {
                    var loop = new CurveLoop();
                    foreach (Curve c in ca)
                        loop.Append(c);

                    loops.Add(loop);
                }
            }

            return loops;
        }
    }
}
