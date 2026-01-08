using Autodesk.Revit.DB;
using Revit26_Plugin.V5_00.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.V5_00.Domain.Services
{
    /// <summary>
    /// Core service that applies slope to roof slab shape vertices.
    /// This is the ONLY place where vertex elevations are modified.
    /// Also persists graph, path results, and final XYZ positions.
    /// </summary>
    public class RoofSlopeProcessorService
    {
        private readonly GraphBuilderService _graphBuilder = new();
        private readonly PathSolverService _pathSolver = new();

        // Persisted data for later pipeline steps
        public Dictionary<SlabShapeVertex, List<SlabShapeVertex>> LastGraph { get; private set; }
        public Dictionary<SlabShapeVertex, (SlabShapeVertex nearest, double dist)> LastPathResults { get; private set; }
        public List<XYZ> LastVertexWorldPoints { get; private set; } = new();

        public (int modified, double maxOffsetMm, double longestPathM) ApplySlopes(
            RoofData roofData,
            List<DrainItem> selectedDrains,
            double slopePercent,
            Action<string> log)
        {
            if (roofData == null || roofData.TopFace == null)
                throw new InvalidOperationException("Roof data is invalid.");

            var editor = roofData.Roof.GetSlabShapeEditor();

            int modifiedCount = 0;
            double maxOffsetMm = 0;
            double longestPathM = 0;

            // 1) Build and persist graph
            log("Building vertex graph...");
            var graph = _graphBuilder.BuildGraph(
                roofData.Vertices,
                roofData.TopFace);
            LastGraph = graph;

            // 2) Identify drain anchor vertices
            log("Identifying drain vertices...");
            var drainVertices = IdentifyDrainVertices(
                roofData.Vertices,
                selectedDrains,
                log);

            // Drain vertices are zero-elevation anchors
            foreach (var dv in drainVertices)
                editor.ModifySubElement(dv, 0.0);

            log($"Drain anchor vertices: {drainVertices.Count}");

            // 3) Run Dijkstra and persist path results
            log("Running Dijkstra path solver...");
            var pathResults =
                _pathSolver.ComputePathsToDrains(
                    graph,
                    drainVertices.ToList());
            LastPathResults = pathResults;

            double slopeRatio = slopePercent / 100.0;

            // 4) Apply vertex elevations
            log("Applying vertex elevations...");

            foreach (var kvp in pathResults)
            {
                var vertex = kvp.Key;
                var distFeet = kvp.Value.dist;

                if (drainVertices.Contains(vertex))
                    continue;

                double elevationFeet = distFeet * slopeRatio;
                double elevationMm = elevationFeet * 304.8;

                editor.ModifySubElement(vertex, elevationFeet);
                modifiedCount++;

                if (elevationMm > maxOffsetMm)
                    maxOffsetMm = elevationMm;

                double pathMeters = distFeet * 0.3048;
                if (pathMeters > longestPathM)
                    longestPathM = pathMeters;
            }

            // 5) Capture final XYZ positions of vertices
            LastVertexWorldPoints = roofData.Vertices
                .Where(v => v.Position != null)
                .Select(v => v.Position)
                .ToList();

            return (modifiedCount, maxOffsetMm, longestPathM);
        }

        // Drain → slab vertex mapping
        private HashSet<SlabShapeVertex> IdentifyDrainVertices(
            List<SlabShapeVertex> vertices,
            List<DrainItem> drains,
            Action<string> log)
        {
            var result = new HashSet<SlabShapeVertex>();

            foreach (var drain in drains)
            {
                double halfW = (drain.Width / 304.8) / 2.0;
                double halfH = (drain.Height / 304.8) / 2.0;

                foreach (var v in vertices)
                {
                    var p = v.Position;
                    if (p == null) continue;

                    if (Math.Abs(p.X - drain.CenterPoint.X) <= halfW &&
                        Math.Abs(p.Y - drain.CenterPoint.Y) <= halfH)
                    {
                        result.Add(v);
                    }
                }
            }

            log($"Mapped {result.Count} vertices to drains.");
            return result;
        }
    }
}
