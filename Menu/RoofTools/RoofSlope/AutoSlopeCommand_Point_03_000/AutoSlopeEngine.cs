using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint.Helpers;
using Revit26_Plugin.AutoSlopeByPoint.Models;
using Revit26_Plugin.AutoSlopeByPoint.Parameters;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Revit26_Plugin.AutoSlopeByPoint.Engine
{
    public static class AutoSlopeEngine
    {
        private const double DRAIN_MATCH_TOL_FT = 0.3; // ~90 mm

        public static void Execute(UIApplication app, AutoSlopePayload data)
        {
            Document doc = app.ActiveUIDocument.Document;
            RoofBase roof = doc.GetElement(data.RoofId) as RoofBase;
            if (roof == null) return;

            SlabShapeEditor editor = roof.GetSlabShapeEditor();

            List<SlabShapeVertex> vertices = new(editor.SlabShapeVertices);

            Face topFace = AutoSlopeGeometry.GetTopFace(roof);
            if (topFace == null)
            {
                data.Log(LogColorHelper.Red("Top face not found."));
                return;
            }

            var dijkstra =
                new DijkstraPathEngine(vertices, topFace, data.ThresholdFt);

            HashSet<int> drainIndices = new();
            for (int i = 0; i < vertices.Count; i++)
            {
                foreach (XYZ d in data.DrainPoints)
                {
                    if (vertices[i].Position.DistanceTo(d) < DRAIN_MATCH_TOL_FT)
                    {
                        drainIndices.Add(i);
                        break;
                    }
                }
            }

            int processed = 0, skipped = 0;
            double maxElevFt = 0, maxPathFt = 0;
            double slopeFactor = data.SlopePercent / 100.0;

            Stopwatch sw = Stopwatch.StartNew();

            using (Transaction tx = new(doc, "AutoSlope"))
            {
                tx.Start();

                foreach (var v in vertices)
                    editor.ModifySubElement(v, 0);

                for (int i = 0; i < vertices.Count; i++)
                {
                    double pathFt = dijkstra.ComputeShortestPath(i, drainIndices);
                    if (double.IsInfinity(pathFt) || pathFt > data.ThresholdFt)
                    {
                        skipped++;
                        continue;
                    }

                    double elevFt = pathFt * slopeFactor;
                    editor.ModifySubElement(vertices[i], elevFt);

                    processed++;
                    maxElevFt = Math.Max(maxElevFt, elevFt);
                    maxPathFt = Math.Max(maxPathFt, pathFt);
                }

                tx.Commit();
            }

            sw.Stop();

            // -------- UI METRIC OUTPUT --------
            data.Vm.VerticesProcessed = processed;
            data.Vm.VerticesSkipped = skipped;
            data.Vm.DrainCount = data.DrainPoints.Count;
            data.Vm.HighestElevation_mm =
                UnitUtils.ConvertFromInternalUnits(maxElevFt, UnitTypeId.Millimeters);
            data.Vm.LongestPath_m =
                UnitUtils.ConvertFromInternalUnits(maxPathFt, UnitTypeId.Meters);
            data.Vm.RunDuration_sec = (int)sw.Elapsed.TotalSeconds;
            data.Vm.RunDate = DateTime.Now.ToString("dd-MM-yy HH:mm");

            AutoSlopeParameterWriter.WriteAll(
                doc, roof, data, maxElevFt, maxPathFt,
                processed, skipped, data.Vm.RunDuration_sec);
        }
    }
}
