using Autodesk.Revit.DB;
using Revit22_Plugin.Asd_V4_01.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.Asd_V4_01.Services
{
    public class RoofSlopeProcessorService
    {
        private readonly GraphBuilderService _graph;
        private readonly PathSolverService _path;

        public RoofSlopeProcessorService()
        {
            _graph = new GraphBuilderService();
            _path = new PathSolverService();
        }

        public (int modifiedCount, double maxOffset, double longestPath)
            ProcessRoofSlopes(
                RoofData roofData,
                List<DrainItem> selectedDrains,
                double slopePercent,
                Action<string> log)
        {
            var doc = roofData.Roof.Document;
            double maxOffset = 0;
            double longest = 0;
            int count = 0;

            using (var tx = new Transaction(doc, "Apply Roof Slopes"))
            {
                tx.Start();

                try
                {
                    log("Building roof graph...");
                    var graph = _graph.BuildGraph(roofData.Vertices, roofData.TopFace);

                    var drainVertices = IdentifyDrainVertices(roofData, selectedDrains, log);

                    foreach (var v in drainVertices)
                        roofData.Roof.GetSlabShapeEditor().ModifySubElement(v, 0.0);

                    log($"Set {drainVertices.Count} drain vertices to zero elevation.");

                    var drainTargets = drainVertices.ToList();

                    log("Computing drainage paths...");
                    var pathResults =
                        _path.ComputePathsToDrains(graph, drainTargets);

                    log("Applying elevations...");

                    foreach (var kvp in pathResults)
                    {
                        var v = kvp.Key;
                        var dist = kvp.Value.dist;

                        if (drainVertices.Contains(v)) continue;

                        double slopeRatio = slopePercent / 100.0;
                        double elevMM = dist * slopeRatio * 304.8;
                        double elevFeet = elevMM / 304.8;

                        roofData.Roof.GetSlabShapeEditor().ModifySubElement(v, elevFeet);

                        if (elevMM > maxOffset) maxOffset = elevMM;
                        double pathMeters = dist * 0.3048;

                        if (pathMeters > longest) longest = pathMeters;

                        count++;
                    }

                    tx.Commit();
                }
                catch (Exception ex)
                {
                    tx.RollBack();
                    log("ERROR: " + ex.Message);
                    throw;
                }
            }

            return (count, maxOffset, longest);
        }

        private HashSet<SlabShapeVertex> IdentifyDrainVertices(
            RoofData roofData,
            List<DrainItem> drains,
            Action<string> log)
        {
            var set = new HashSet<SlabShapeVertex>();

            foreach (var drain in drains)
            {
                log($"Finding drain area vertices for: {drain.SizeCategory}");

                double w = drain.Width / 304.8;
                double h = drain.Height / 304.8;

                double minX = drain.CenterPoint.X - w / 2;
                double maxX = drain.CenterPoint.X + w / 2;
                double minY = drain.CenterPoint.Y - h / 2;
                double maxY = drain.CenterPoint.Y + h / 2;

                foreach (var v in roofData.Vertices)
                {
                    if (v == null || v.Position == null) continue;

                    var p = v.Position;

                    if (p.X >= minX && p.X <= maxX &&
                        p.Y >= minY && p.Y <= maxY)
                    {
                        set.Add(v);
                    }
                }
            }

            log($"Total drain vertices: {set.Count}");
            return set;
        }
    }
}
