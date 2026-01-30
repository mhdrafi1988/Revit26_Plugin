// =====================================================
// File: RoofSlopeProcessorService.cs
// Purpose: Apply slope to roof using graph + Dijkstra
// =====================================================

using Autodesk.Revit.DB;
using Revit22_Plugin.V4_02.Domain.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit22_Plugin.V4_02.Domain.Services
{
    /// <summary>
    /// Core service that applies slope to roof slab shape vertices.
    /// This is the ONLY place where vertex elevations are modified.
    /// </summary>
    public class RoofSlopeProcessorService
    {
        private readonly GraphBuilderService _graphBuilder = new();
        private readonly PathSolverService _pathSolver = new();

        public (int modified, double maxOffsetMm, double longestPathM)
            ApplySlopes(
                RoofData roofData,
                List<DrainItem> selectedDrains,
                double slopePercent,
                Action<string> log)
        {
            if (roofData == null || roofData.TopFace == null)
                throw new InvalidOperationException("Roof data is invalid.");

            var doc = roofData.Roof.Document;
            var editor = roofData.Roof.GetSlabShapeEditor();

            int modifiedCount = 0;
            double maxOffsetMm = 0;
            double longestPathM = 0;

            log("Building vertex graph...");
            var graph = _graphBuilder.BuildGraph(
                roofData.Vertices,
                roofData.TopFace);

            log("Identifying drain vertices...");
            var drainVertices = IdentifyDrainVertices(
                roofData.Vertices,
                selectedDrains,
                log);

            // Drain vertices are zero-elevation anchors
            foreach (var dv in drainVertices)
                editor.ModifySubElement(dv, 0.0);

            log($"Drain anchor vertices: {drainVertices.Count}");

            log("Running Dijkstra path solver...");
            var pathResults =
                _pathSolver.ComputePathsToDrains(
                    graph,
                    drainVertices.ToList());

            double slopeRatio = slopePercent / 100.0;

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

            return (modifiedCount, maxOffsetMm, longestPathM);
        }

        // -------------------------------------------------
        // Drain → slab vertex mapping
        // -------------------------------------------------
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
