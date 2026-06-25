using Autodesk.Revit.DB;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;

namespace Revit26_Plugin.AutoSlopeByPoint.V009.Infrastructure.Helpers
{
    public static class DrainDetectionHelper
    {
        public static List<XYZ> DetectDrainsWithinRadius(
            RoofBase roof,
            List<XYZ> selectedPoints,
            double toleranceMm,
            Action<LogEntry> log)
        {
            var result = new List<XYZ>();

            if (roof == null)
            {
                log?.Invoke(new LogEntry(LogLevel.Warning, "Drain tolerance skipped: roof is null."));
                return result;
            }

            if (selectedPoints == null || selectedPoints.Count == 0)
            {
                log?.Invoke(new LogEntry(LogLevel.Warning, "Drain tolerance skipped: no user-picked points were provided."));
                return result;
            }

            result.AddRange(selectedPoints);

            if (toleranceMm <= 0)
                return result;

            double toleranceFt = UnitUtils.ConvertToInternalUnits(toleranceMm, UnitTypeId.Millimeters);
            var addedKeys = new HashSet<string>();
            foreach (XYZ point in result)
                if (point != null) addedKeys.Add(GetPointKey(point, toleranceFt));

            SlabShapeEditor editor;
            try { editor = roof.GetSlabShapeEditor(); }
            catch (Exception ex)
            {
                log?.Invoke(new LogEntry(LogLevel.Error,
                    $"Drain tolerance skipped: failed to access slab shape editor. {ex.Message}"));
                return result;
            }

            if (editor == null || !editor.IsValidObject)
            {
                log?.Invoke(new LogEntry(LogLevel.Warning,
                    "Drain tolerance skipped: roof slab shape editor is not available."));
                return result;
            }

            List<SlabShapeVertex> roofVertices;
            try
            {
                roofVertices = new List<SlabShapeVertex>();
                var arr = editor.SlabShapeVertices;
                for (int i = 0; i < arr.Size; i++) roofVertices.Add(arr.get_Item(i));
            }
            catch (Exception ex)
            {
                log?.Invoke(new LogEntry(LogLevel.Error,
                    $"Drain tolerance skipped: failed to read slab shape vertices. {ex.Message}"));
                return result;
            }

            if (roofVertices == null || roofVertices.Count == 0)
            {
                log?.Invoke(new LogEntry(LogLevel.Warning,
                    "Drain tolerance skipped: roof has no slab-shape vertices."));
                return result;
            }

            log?.Invoke(new LogEntry(LogLevel.Info,
                $"Checking roof slab-shape vertices within {toleranceMm:0} mm of user-picked points..."));

            int foundCount = 0;
            foreach (XYZ selectedPoint in selectedPoints)
            {
                if (selectedPoint == null) continue;
                foreach (SlabShapeVertex vertex in roofVertices)
                {
                    if (vertex == null || !vertex.IsValidObject) continue;
                    XYZ vp = vertex.Position;
                    if (vp == null || selectedPoint.DistanceTo(vp) > toleranceFt) continue;
                    string key = GetPointKey(vp, toleranceFt);
                    if (addedKeys.Contains(key)) continue;
                    addedKeys.Add(key);
                    result.Add(vp);
                    foundCount++;
                    log?.Invoke(new LogEntry(LogLevel.Info,
                        $"  • Added nearby roof shape point at ({vp.X:F3}, {vp.Y:F3}, {vp.Z:F3})"));
                }
            }

            if (foundCount > 0)
            {
                log?.Invoke(new LogEntry(LogLevel.Success, $"✅ Added {foundCount} nearby roof shape point(s)."));
                log?.Invoke(new LogEntry(LogLevel.Info,    $"   Total drain points: {result.Count}"));
            }
            else
            {
                log?.Invoke(new LogEntry(LogLevel.Info,
                    $"ℹ️ No roof shape points found within {toleranceMm:0} mm of the picked points."));
            }

            return result;
        }

        public static List<XYZ> RemoveDuplicates(List<XYZ> points, double toleranceMm)
        {
            if (points == null || points.Count == 0) return new List<XYZ>();
            if (points.Count == 1) return new List<XYZ>(points);
            double toleranceFt = UnitUtils.ConvertToInternalUnits(toleranceMm, UnitTypeId.Millimeters);
            if (toleranceFt <= 0) return new List<XYZ>(points);
            var unique = new List<XYZ>();
            var seen   = new HashSet<string>();
            foreach (XYZ p in points)
            {
                if (p == null) continue;
                if (seen.Add(GetPointKey(p, toleranceFt))) unique.Add(p);
            }
            return unique;
        }

        private static string GetPointKey(XYZ point, double toleranceFt)
        {
            const double bucket = 0.03240420;
            double x = Math.Round(point.X / bucket) * bucket;
            double y = Math.Round(point.Y / bucket) * bucket;
            double z = Math.Round(point.Z / bucket) * bucket;
            return $"{x:F6},{y:F6},{z:F6}";
        }
    }
}
