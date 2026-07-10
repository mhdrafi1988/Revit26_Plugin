using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;

namespace Revit26_Plugin.FloorsAndRoofFromLinkedRoomsViaPlanView.V003
{
    public enum RoofCreationTier
    {
        RawSegments,            // Method 1 (confirmed with Rafi) — raw un-deduped boundary
                                 // segments straight from the room, transformed, no rebuild.
        Direct,                 // our deduped/validated CurveLoop, as-is
        ZFlattened,             // Direct loop, points snapped to level elevation
        ReversedWinding,        // ZFlattened loop, curve order/direction reversed
        BoundingBoxApproximate  // rectangle from the loop's bounding box — CHANGES SHAPE
    }

    public class RoofCreationResult
    {
        public FootPrintRoof Roof { get; set; }
        public RoofCreationTier Tier { get; set; }
    }

    /// <summary>
    /// ASSUMPTION (flagged): uses the classic instance API
    /// doc.Create.NewFootPrintRoof(CurveArray, Level, RoofType, out ModelCurveArray).
    /// Confirmed via public documentation that roofs never received a modern static
    /// Create() replacement the way Floor/Wall did, so this remains the correct call.
    ///
    /// LIMITATION (flagged): only the OUTER boundary loop is ever used across all tiers.
    ///
    /// FALLBACK CHAIN (flagged, confirmed with Rafi):
    ///   Tier 0 RawSegments          — "Method 1": room.GetBoundarySegments() curves used
    ///                                 directly (transformed), no dedupe/trim/rebuild — this
    ///                                 mirrors a previously working Revit22 tool of Rafi's.
    ///   Tier 1 Direct               — our normal deduped/validated CurveLoop (same one used
    ///                                 for floors), as produced by RoomBoundaryService.
    ///   Tier 2 ZFlattened           — Tier 1's loop, points snapped to level.Elevation exactly.
    ///   Tier 3 ReversedWinding      — same flattened points, curves reversed/re-ordered.
    ///   Tier 4 BoundingBoxApproximate — rectangle from the loop's XY bounding box. CHANGES
    ///                                 the roof's actual shape/area — always logged distinctly.
    ///   If all five fail, an exception listing every tier's own error is thrown, so the
    ///   per-room catch block in the handler logs it as a normal skip.
    ///
    ///   NOTE: per Rafi's decision, there is no "draw sketch lines only" backstop — only a
    ///   real Roof element counts as success.
    /// </summary>
    public static class RoofCreationService
    {
        /// <summary>Builds a plain checklist of every input going into roof creation, for
        /// logging to the UI before the attempt — one line per item, Yes/No plus the actual
        /// value, so the person can see at a glance what was actually received.</summary>
        public static List<string> DescribeInputs(Document doc, Room room, Transform linkTransform,
            CurveLoop rawSegmentsLoop, CurveLoop dedupedLoop, ElementId roofTypeId, Level level)
        {
            var lines = new List<string>();

            lines.Add($"Room received: {(room != null ? "Yes" : "No")}" +
                (room != null ? $" (Id={room.Id}, Name='{room.Name}')" : ""));

            lines.Add($"Link transform received: {(linkTransform != null ? "Yes" : "No")}");

            lines.Add($"Raw-segments profile received: {(rawSegmentsLoop != null ? "Yes" : "No")}" +
                (rawSegmentsLoop != null ? $" ({CountCurves(rawSegmentsLoop)} curves, valid={IsLoopValid(rawSegmentsLoop)})" : ""));

            lines.Add($"Deduped profile received: {(dedupedLoop != null ? "Yes" : "No")}" +
                (dedupedLoop != null ? $" ({CountCurves(dedupedLoop)} curves, valid={IsLoopValid(dedupedLoop)})" : ""));

            var level_ = level;
            lines.Add($"Level received: {(level_ != null ? "Yes" : "No")}" +
                (level_ != null ? $" (Id={level_.Id}, Name='{level_.Name}', Elevation={level_.Elevation:F2})" : ""));

            var roofType = doc.GetElement(roofTypeId) as RoofType;
            lines.Add($"Roof type received: {(roofType != null ? "Yes" : "No")}" +
                (roofType != null ? $" (Id={roofType.Id}, Name='{roofType.Name}', HasCompoundStructure={roofType.GetCompoundStructure() != null})" : ""));

            lines.Add($"doc.Create available: {(doc.Create != null ? "Yes" : "No")}");
            lines.Add($"doc.IsFamilyDocument: {doc.IsFamilyDocument}");

            return lines;
        }

        private static int CountCurves(CurveLoop loop)
        {
            int n = 0;
            foreach (var _ in loop) n++;
            return n;
        }

        private static bool IsLoopValid(CurveLoop loop)
        {
            if (loop == null) return false;
            try
            {
                return !loop.IsOpen() && CountCurves(loop) >= 3;
            }
            catch
            {
                return false;
            }
        }

        public static RoofCreationResult Create(Document doc, Room room, Transform linkTransform,
            CurveLoop outerLoop, ElementId roofTypeId, Level level)
        {
            var roofType = doc.GetElement(roofTypeId) as RoofType;
            if (roofType == null)
                throw new InvalidOperationException(
                    $"Roof type with id {roofTypeId} was not found in the document (GetElement returned null or the wrong type).");
            if (roofType.GetCompoundStructure() == null)
                throw new InvalidOperationException(
                    $"Roof type '{roofType.Name}' has no compound structure (e.g. sloped glazing) and cannot host a footprint roof. Pick a basic roof type.");
            if (level == null)
                throw new InvalidOperationException("Target level was null — active view has no associated level.");

            var tierErrors = new List<string>();

            // Tier 0 — Method 1: raw boundary segments, no dedupe/rebuild.
            try
            {
                var rawLoop = BuildRawLoop(room, linkTransform);
                var rawFootprint = ToCurveArray(rawLoop);
                var roof = CreateFromFootprint(doc, rawFootprint, roofType, level);
                return new RoofCreationResult { Roof = roof, Tier = RoofCreationTier.RawSegments };
            }
            catch (Exception ex0)
            {
                tierErrors.Add("RawSegments: " + ExceptionFormatting.Full(ex0));
            }

            // Tier 1 — our deduped/validated loop, as-is.
            try
            {
                var roof = CreateFromLoop(doc, outerLoop, roofType, level);
                return new RoofCreationResult { Roof = roof, Tier = RoofCreationTier.Direct };
            }
            catch (Exception ex1)
            {
                tierErrors.Add("Direct: " + ExceptionFormatting.Full(ex1));
            }

            CurveLoop flattened = FlattenZ(outerLoop, level.Elevation);
            try
            {
                var roof = CreateFromLoop(doc, flattened, roofType, level);
                return new RoofCreationResult { Roof = roof, Tier = RoofCreationTier.ZFlattened };
            }
            catch (Exception ex2)
            {
                tierErrors.Add("ZFlattened: " + ExceptionFormatting.Full(ex2));
            }

            try
            {
                var reversed = ReverseLoop(flattened);
                var roof = CreateFromLoop(doc, reversed, roofType, level);
                return new RoofCreationResult { Roof = roof, Tier = RoofCreationTier.ReversedWinding };
            }
            catch (Exception ex3)
            {
                tierErrors.Add("ReversedWinding: " + ExceptionFormatting.Full(ex3));
            }

            try
            {
                var bboxLoop = BuildBoundingBoxLoop(outerLoop, level.Elevation);
                var roof = CreateFromLoop(doc, bboxLoop, roofType, level);
                return new RoofCreationResult { Roof = roof, Tier = RoofCreationTier.BoundingBoxApproximate };
            }
            catch (Exception ex4)
            {
                tierErrors.Add("BoundingBox: " + ExceptionFormatting.Full(ex4));
            }

            throw new InvalidOperationException(
                $"All roof creation tiers failed. Tier errors: {string.Join(" || ", tierErrors)}");
        }

        /// <summary>Method 1 (confirmed with Rafi) — builds the footprint straight from the
        /// room's own boundary segments, transformed into host coordinates, with no dedupe,
        /// trim, or rebuild. Mirrors a previously-working tool. Public so the handler can
        /// reuse it for the UI-log checklist without duplicating logic.</summary>
        public static CurveLoop BuildRawLoop(Room room, Transform transform)
        {
            var options = new SpatialElementBoundaryOptions();
            var boundaries = room.GetBoundarySegments(options);

            var loop = new CurveLoop();
            if (boundaries == null || boundaries.Count == 0) return loop;

            foreach (var segment in boundaries[0])
            {
                var c = segment.GetCurve();
                if (c != null)
                    loop.Append(c.CreateTransformed(transform));
            }
            return loop;
        }

        private static CurveArray ToCurveArray(CurveLoop loop)
        {
            var footprint = new CurveArray();
            foreach (var curve in loop)
                footprint.Append(curve);
            return footprint;
        }

        private static FootPrintRoof CreateFromLoop(Document doc, CurveLoop loop, RoofType roofType, Level level)
        {
            var footprint = ToCurveArray(loop);
            return CreateFromFootprint(doc, footprint, roofType, level);
        }

        private static FootPrintRoof CreateFromFootprint(Document doc, CurveArray footprint, RoofType roofType, Level level)
        {
            string diagnostic = $"footprint.Size={footprint.Size}, level.Id={level?.Id}, " +
                $"roofType.Id={roofType?.Id}, roofType.Name='{roofType?.Name}', " +
                $"doc.Create null={doc.Create == null}, doc.IsFamilyDocument={doc.IsFamilyDocument}";

            try
            {
                return doc.Create.NewFootPrintRoof(footprint, level, roofType, out _);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"NewFootPrintRoof failed [{diagnostic}]", ex);
            }
        }

        /// <summary>Rebuilds the loop with every point's Z snapped to targetZ. Only straight
        /// segments are handled precisely; arcs are flattened by moving their endpoints only,
        /// which is an approximation for curved boundaries (flagged — not exact arc geometry).</summary>
        private static CurveLoop FlattenZ(CurveLoop loop, double targetZ)
        {
            var points = new List<XYZ>();
            foreach (var curve in loop)
            {
                var p = curve.GetEndPoint(0);
                points.Add(new XYZ(p.X, p.Y, targetZ));
            }
            var last = loop.Last().GetEndPoint(1);
            points.Add(new XYZ(last.X, last.Y, targetZ));

            var result = new CurveLoop();
            for (int i = 0; i < points.Count - 1; i++)
                result.Append(Line.CreateBound(points[i], points[i + 1]));
            return result;
        }

        private static CurveLoop ReverseLoop(CurveLoop loop)
        {
            var curves = loop.ToList();
            curves.Reverse();
            var result = new CurveLoop();
            foreach (var c in curves)
                result.Append(c.CreateReversed());
            return result;
        }

        /// <summary>Last-resort approximation — rectangle from the loop's XY bounding box.
        /// This does NOT preserve the room's actual shape or area; callers must log this
        /// distinctly, never as a normal trim/fix.</summary>
        private static CurveLoop BuildBoundingBoxLoop(CurveLoop loop, double z)
        {
            var points = loop.SelectMany(c => new[] { c.GetEndPoint(0), c.GetEndPoint(1) }).ToList();
            double minX = points.Min(p => p.X), maxX = points.Max(p => p.X);
            double minY = points.Min(p => p.Y), maxY = points.Max(p => p.Y);

            var p1 = new XYZ(minX, minY, z);
            var p2 = new XYZ(maxX, minY, z);
            var p3 = new XYZ(maxX, maxY, z);
            var p4 = new XYZ(minX, maxY, z);

            var result = new CurveLoop();
            result.Append(Line.CreateBound(p1, p2));
            result.Append(Line.CreateBound(p2, p3));
            result.Append(Line.CreateBound(p3, p4));
            result.Append(Line.CreateBound(p4, p1));
            return result;
        }
    }
}
