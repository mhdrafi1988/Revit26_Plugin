// =======================================================
// File: LoopDivisionService.cs
// Location: Core/Services/
// Changes vs V004: AddDivisionPoints now returns List<LogEntry> (Shared
// LogEntry/LogLevel model) instead of List<string>, matching the logging
// convention used everywhere else in the suite. Message text is
// unchanged — only the container/level type changed.
// =======================================================

using Autodesk.Revit.DB;
using Revit26_Plugin.RoofLoopAnalyzerPDC.V005.Core.Models;
using Revit26_Plugin.Shared.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.RoofLoopAnalyzerPDC.V005.Core.Services
{
    public class LoopDivisionService
    {
        /// <summary>
        /// Adds division points to the roof slab shape editor for each selected loop.
        /// Returns a list of log entries describing success or failure per loop.
        /// </summary>
        public List<LogEntry> AddDivisionPoints(Document doc, RoofBase roof, IEnumerable<RoofLoopModel> loops)
        {
            var log = new List<LogEntry>();
            void Add(LogLevel lvl, string m) => log.Add(new LogEntry(lvl, m));

            if (doc == null)   { Add(LogLevel.Error, "Document is null.");  return log; }
            if (roof == null)  { Add(LogLevel.Error, "Roof is null.");       return log; }
            if (loops == null) { Add(LogLevel.Error, "Loop list is null.");  return log; }

            var validLoops = loops.Where(l => l != null && l.IsSelected && l.RecommendedPoints > 0).ToList();

            if (!validLoops.Any())
            {
                Add(LogLevel.Warning, "No loops selected for division.");
                return log;
            }

            using (Transaction tx = new Transaction(doc, "Add Division Points"))
            {
                tx.Start();

                SlabShapeEditor editor = roof.GetSlabShapeEditor();
                if (!editor.IsEnabled)
                    editor.Enable();

                foreach (var loop in validLoops)
                {
                    if (loop.Geometry == null)
                    {
                        Add(LogLevel.Warning, $"Loop {loop.Index} ({loop.LoopShapeType}): Skipped — geometry is null.");
                        continue;
                    }

                    try
                    {
                        int pointsAdded = loop.LoopShapeType == "Circular"
                            ? AddCircularPoints(editor, loop, log)
                            : AddParametricPoints(editor, loop, log);

                        Add(LogLevel.Success, $"Loop {loop.Index} ({loop.LoopShapeType}): {pointsAdded} point(s) added.");
                    }
                    catch (Exception ex)
                    {
                        Add(LogLevel.Error, $"Loop {loop.Index} ({loop.LoopShapeType}): Exception — {ex.Message}");
                    }
                }

                tx.Commit();
            }

            return log;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // CIRCULAR: distribute n points evenly by angle around the circle center
        // ─────────────────────────────────────────────────────────────────────────
        private int AddCircularPoints(SlabShapeEditor editor, RoofLoopModel loop, List<LogEntry> log)
        {
            int n          = loop.RecommendedPoints;
            XYZ center     = loop.Center;
            double radius  = loop.Radius;
            double z       = center.Z;

            // Determine the Z level from the first curve of the geometry
            // (center.Z from arcs may be at a different elevation than the face)
            var firstCurve = loop.Geometry.FirstOrDefault();
            if (firstCurve != null)
                z = firstCurve.GetEndPoint(0).Z;

            double angleStep = 2 * Math.PI / n;   // e.g. 8 points → 45° apart
            int pointsAdded  = 0;

            for (int i = 0; i < n; i++)
            {
                double angle = i * angleStep;
                double x     = center.X + radius * Math.Cos(angle);
                double y     = center.Y + radius * Math.Sin(angle);
                XYZ point    = new XYZ(x, y, z);

                try
                {
                    editor.AddPoint(point);
                    pointsAdded++;
                }
                catch (Autodesk.Revit.Exceptions.ArgumentException)
                {
                    log.Add(new LogEntry(LogLevel.Info,
                        $"  ↳ Loop {loop.Index}: Point at angle {Math.Round(angle * 180 / Math.PI, 1)}° already exists — skipped."));
                }
            }

            return pointsAdded;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // RECTANGLE / OTHER: distribute n points evenly along each curve by parameter
        // ─────────────────────────────────────────────────────────────────────────
        private int AddParametricPoints(SlabShapeEditor editor, RoofLoopModel loop, List<LogEntry> log)
        {
            int n           = loop.RecommendedPoints;
            int pointsAdded = 0;

            var curves = loop.Geometry.ToList();
            if (!curves.Any()) return 0;

            // Spread n points evenly across all curves using their arc-length proportions
            double totalLength = curves.Sum(c => c.Length);

            foreach (Curve curve in curves)
            {
                // How many points belong proportionally on this curve
                double fraction      = curve.Length / totalLength;
                int pointsOnCurve    = Math.Max(1, (int)Math.Round(n * fraction));

                for (int i = 0; i < pointsOnCurve; i++)
                {
                    // Distribute across the interior of the curve (exclude endpoints to avoid duplicates at joins)
                    double param = (i + 1.0) / (pointsOnCurve + 1.0);
                    XYZ point    = curve.Evaluate(param, true);

                    try
                    {
                        editor.AddPoint(point);
                        pointsAdded++;
                    }
                    catch (Autodesk.Revit.Exceptions.ArgumentException)
                    {
                        log.Add(new LogEntry(LogLevel.Info,
                            $"  ↳ Loop {loop.Index}: Point at param {Math.Round(param, 3)} already exists — skipped."));
                    }
                }
            }

            return pointsAdded;
        }
    }
}
