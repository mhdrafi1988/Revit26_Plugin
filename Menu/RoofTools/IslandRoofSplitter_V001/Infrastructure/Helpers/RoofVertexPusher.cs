// IslandRoofSplitter V001 — Sloped mode: push each sketch vertex of a freshly created flat roof to the parent
// roof's Z, and add interior ridge/valley points. Shape-editing (sub-element) technique, Revit 2026 API:
// RoofBase.GetSlabShapeEditor() (the old RoofBase.SlabShapeEditor property was removed in 2026),
// SlabShapeEditor.AddPoint(XYZ) and ModifySubElement(SlabShapeVertex, offset).
// VERIFY against RoofPointElevationSync / CompareRoofs: if those tools compute the offset differently, align here.
// Caller owns the Transaction.
#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Revit26_Plugin_Revised.IslandRoofSplitter.V001.Core.Models;

namespace Revit26_Plugin_Revised.IslandRoofSplitter.V001.Infrastructure.Helpers
{
    public static class RoofVertexPusher
    {
        private static readonly double MatchTolFt = LengthUnits.MmToFt(5);

        /// <returns>null on success, otherwise the failure reason.</returns>
        public static string? Push(Document doc, FootPrintRoof roof, Island island)
        {
            doc.Regenerate();
            SlabShapeEditor ed = roof.GetSlabShapeEditor();
            if (ed == null) return "Vertex-Z push failed: roof has no shape editor";
            if (!ed.IsEnabled) ed.Enable();
            doc.Regenerate();

            var verts = ed.SlabShapeVertices.Cast<SlabShapeVertex>().ToList();
            if (verts.Count == 0) return "Vertex-Z push failed: no editable vertices after enabling shape editing";

            // Flat roof → every original vertex has the same top elevation; offsets are measured from it.
            double baseTop = verts.Average(v => v.Position.Z);

            var targets = new List<(Pt2 xy, double z, bool add)>();
            foreach (var loop in new[] { island.Outer }.Concat(island.Holes))
                for (int i = 0; i < loop.Edges.Count; i++)
                {
                    if (loop.Z[i] is not double zz)
                        return $"Vertex-Z push failed: no roof Z at {Fmt(loop.Edges[i].Start)}";
                    targets.Add((loop.Edges[i].Start, zz, false));
                }
            foreach (var p in island.InteriorPoints) targets.Add((p.XY, p.Z, true));

            int pushed = 0;
            foreach (var (xy, z, add) in targets)
            {
                SlabShapeVertex? v;
                if (add)
                {
                    v = ed.AddPoint(new XYZ(xy.X, xy.Y, baseTop));
                    if (v == null) return $"Vertex-Z push failed: could not add interior point at {Fmt(xy)}";
                }
                else
                {
                    v = verts.OrderBy(sv => Dist(sv.Position, xy)).FirstOrDefault();
                    if (v == null || Dist(v.Position, xy) > MatchTolFt)
                        return $"Vertex-Z push failed: sketch vertex not found at {Fmt(xy)}";
                }
                ed.ModifySubElement(v, z - baseTop);
                pushed++;
            }
            return null;
        }

        private static double Dist(XYZ p, Pt2 q) => Math.Sqrt((p.X - q.X) * (p.X - q.X) + (p.Y - q.Y) * (p.Y - q.Y));
        private static string Fmt(Pt2 p) => $"({p.X * LengthUnits.MmPerFoot:0} mm, {p.Y * LengthUnits.MmPerFoot:0} mm)";
    }
}
