using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPoint_04.Infrastructure.Helpers
{
    public static class DrainDetectionHelper
    {
        /// <summary>
        /// Expands user-picked drain points by including nearby roof slab-shape vertices
        /// within the specified tolerance radius.
        /// 
        /// IMPORTANT:
        /// - No model scan
        /// - No FamilyInstance lookup
        /// - Only uses the selected roof's slab-shape vertices
        /// </summary>
        public static List<XYZ> DetectDrainsWithinRadius(
            RoofBase roof,
            List<XYZ> selectedPoints,
            double toleranceMm,
            Action<string> log)
        {
            var result = new List<XYZ>();

            if (roof == null)
            {
                log?.Invoke("Drain tolerance skipped: roof is null.");
                return result;
            }

            if (selectedPoints == null || selectedPoints.Count == 0)
            {
                log?.Invoke("Drain tolerance skipped: no user-picked points were provided.");
                return result;
            }

            // Always preserve user-picked points.
            result.AddRange(selectedPoints);

            if (toleranceMm <= 0)
            {
                return result;
            }

            double toleranceFt = UnitUtils.ConvertToInternalUnits(toleranceMm, UnitTypeId.Millimeters);
            var addedKeys = new HashSet<string>();

            foreach (XYZ point in result)
            {
                if (point == null)
                    continue;

                addedKeys.Add(GetPointKey(point, toleranceFt));
            }

            SlabShapeEditor editor;
            try
            {
                editor = roof.GetSlabShapeEditor();
            }
            catch (Exception ex)
            {
                log?.Invoke($"Drain tolerance skipped: failed to access slab shape editor. {ex.Message}");
                return result;
            }

            if (editor == null || !editor.IsValidObject)
            {
                log?.Invoke("Drain tolerance skipped: roof slab shape editor is not available.");
                return result;
            }

            List<SlabShapeVertex> roofVertices;
            try
            {
                roofVertices = new List<SlabShapeVertex>();
                var slabShapeVertexArray = editor.SlabShapeVertices;
                for (int i = 0; i < slabShapeVertexArray.Size; i++)
                {
                    var vertex = slabShapeVertexArray.get_Item(i);
                    roofVertices.Add(vertex);
                }
            }
            catch (Exception ex)
            {
                log?.Invoke($"Drain tolerance skipped: failed to read slab shape vertices. {ex.Message}");
                return result;
            }

            if (roofVertices == null || roofVertices.Count == 0)
            {
                log?.Invoke("Drain tolerance skipped: roof has no slab-shape vertices.");
                return result;
            }

            log?.Invoke($"Checking roof slab-shape vertices within {toleranceMm:0} mm of user-picked points...");

            int foundCount = 0;

            foreach (XYZ selectedPoint in selectedPoints)
            {
                if (selectedPoint == null)
                    continue;

                foreach (SlabShapeVertex vertex in roofVertices)
                {
                    if (vertex == null || !vertex.IsValidObject)
                        continue;

                    XYZ vertexPoint = vertex.Position;
                    if (vertexPoint == null)
                        continue;

                    if (selectedPoint.DistanceTo(vertexPoint) > toleranceFt)
                        continue;

                    string key = GetPointKey(vertexPoint, toleranceFt);
                    if (addedKeys.Contains(key))
                        continue;

                    addedKeys.Add(key);
                    result.Add(vertexPoint);
                    foundCount++;

                    log?.Invoke(
                        $"  • Added nearby roof shape point at " +
                        $"({vertexPoint.X:F3}, {vertexPoint.Y:F3}, {vertexPoint.Z:F3})");
                }
            }

            if (foundCount > 0)
            {
                log?.Invoke($"✅ Added {foundCount} nearby roof shape point(s).");
                log?.Invoke($"   Total drain points: {result.Count}");
            }
            else
            {
                log?.Invoke($"ℹ️ No roof shape points found within {toleranceMm:0} mm of the picked points.");
            }

            return result;
        }

        /// <summary>
        /// Removes duplicate points using the same tolerance bucket logic.
        /// </summary>
        public static List<XYZ> RemoveDuplicates(List<XYZ> points, double toleranceMm)
        {
            if (points == null || points.Count == 0)
                return new List<XYZ>();

            if (points.Count == 1)
                return new List<XYZ>(points);

            double toleranceFt = UnitUtils.ConvertToInternalUnits(toleranceMm, UnitTypeId.Millimeters);
            if (toleranceFt <= 0)
                return new List<XYZ>(points);

            var uniquePoints = new List<XYZ>();
            var seenKeys = new HashSet<string>();

            foreach (XYZ point in points)
            {
                if (point == null)
                    continue;

                string key = GetPointKey(point, toleranceFt);
                if (seenKeys.Add(key))
                {
                    uniquePoints.Add(point);
                }
            }

            return uniquePoints;
        }

        /// <summary>
        /// Creates a stable tolerance-based key for geometric duplicate detection.
        /// </summary>
        private static string GetPointKey(XYZ point, double toleranceFt)
        {
            double bucket = Math.Max(toleranceFt * 0.5, 1e-9);

            double x = Math.Round(point.X / bucket) * bucket;
            double y = Math.Round(point.Y / bucket) * bucket;
            double z = Math.Round(point.Z / bucket) * bucket;

            return $"{x:F6},{y:F6},{z:F6}";
        }
    }
}