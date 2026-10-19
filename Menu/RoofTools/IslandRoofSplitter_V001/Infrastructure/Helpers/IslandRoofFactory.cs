// IslandRoofSplitter V001 — creates one footprint roof per island (no slope-defining edges), sets type, level,
// base offset and Mark. Openings (Ring mode) are passed as extra closed loops in the same footprint CurveArray.
// Caller owns the Transaction.
#nullable enable
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Revit26_Plugin_Revised.IslandRoofSplitter.V001.Core.Models;

namespace Revit26_Plugin_Revised.IslandRoofSplitter.V001.Infrastructure.Helpers
{
    public static class IslandRoofFactory
    {
        public static FootPrintRoof Create(Document doc, Island island, Level level, RoofType type,
                                           double baseOffsetFt, string mark)
        {
            double z = level.Elevation;
            var footprint = new CurveArray();
            AddLoop(footprint, island.Outer.Edges, z);
            foreach (var hole in island.Holes) AddLoop(footprint, hole.Edges, z);

            FootPrintRoof roof = doc.Create.NewFootPrintRoof(footprint, level, type, out ModelCurveArray map);
            foreach (ModelCurve mc in map)
                roof.set_DefinesSlope(mc, false);

            roof.get_Parameter(BuiltInParameter.ROOF_LEVEL_OFFSET_PARAM)?.Set(baseOffsetFt);
            var markParam = roof.get_Parameter(BuiltInParameter.ALL_MODEL_MARK);
            if (markParam != null && !markParam.IsReadOnly) markParam.Set(mark);
            return roof;
        }

        private static void AddLoop(CurveArray arr, List<LoopEdge> edges, double z)
        {
            foreach (var e in edges)
            {
                var s = new XYZ(e.Start.X, e.Start.Y, z);
                var t = new XYZ(e.End.X, e.End.Y, z);
                if (e.Mid is Pt2 m)
                    arr.Append(Arc.Create(s, t, new XYZ(m.X, m.Y, z)));
                else
                    arr.Append(Line.CreateBound(s, t));
            }
        }
    }
}
