using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Revit22_Plugin.Asd_V4_01.payloads;
//using Revit22_Plugin.AutoSlope_V4_01.Engine;
using Revit22_Plugin.Asd_V4_01.Services;
using Revit22_Plugin.AutoSlope_V4_01.Helpers;
using Revit22_Plugin.Asd_V4_01.Services;
using Revit22_Plugin.AutoSlope_04_01.Engines;

namespace Revit22_Plugin.AutoSlope_V4_01.Engine
{
    public static class AutoSlopeEngine
    {
        public static void Execute(UIApplication uiapp, AutoSlopePayload_04_01 data)
        {
            Document doc = uiapp.ActiveUIDocument.Document;

            data.Log(LogColorHelper.Cyan("===== AutoSlope Engine Started ====="));

            RoofBase roof = doc.GetElement(data.RoofId) as RoofBase;
            if (roof == null)
            {
                data.Log(LogColorHelper.Red("Invalid roof ID passed to engine."));
                return;
            }

            data.Log(LogColorHelper.Black($"Roof ElementId: {roof.Id.Value}"));

            SlabShapeEditor editor = roof.GetSlabShapeEditor();
            if (!editor.IsEnabled)
            {
                data.Log(LogColorHelper.Red("Shape editing is NOT enabled. Command must enable it first."));
                return;
            }

            // -------------------------------------------------------
            // COLLECT VERTICES
            // -------------------------------------------------------
            List<SlabShapeVertex> vertices = new List<SlabShapeVertex>();
            foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                vertices.Add(v);

            data.Log(LogColorHelper.Black($"Total vertices: {vertices.Count}"));
            data.Log(LogColorHelper.Black($"Drain points: {data.DrainPoints.Count}"));
            data.Log(LogColorHelper.Black($"Slope %: {data.SlopePercent}"));
            data.Log(LogColorHelper.Black($"Threshold: {data.ThresholdMeters} meters"));

            double slopeFactor = data.SlopePercent / 100.0;
            double thresholdFt = UnitUtils.ConvertToInternalUnits(
                data.ThresholdMeters,
                UnitTypeId.Meters);

            // -------------------------------------------------------
            // GET TOP FACE (for void avoidance)
            // -------------------------------------------------------
            Face topFace = AutoSlopeGeometry.GetTopFace(roof);
            if (topFace == null)
            {
                data.Log(LogColorHelper.Red("Could not determine the roof's top face."));
                return;
            }

            // -------------------------------------------------------
            // BUILD DIJKSTRA GRAPH
            // -------------------------------------------------------
            data.Log(LogColorHelper.Black("Building void-aware graph..."));

            var dijkstra = new DijkstraPathEngine(
                vertices,
                topFace,
                thresholdFt  // edge threshold
            );

            // -------------------------------------------------------
            // MAP DRAIN POINTS → VERTEX INDICES
            // -------------------------------------------------------
            HashSet<int> drainIndexSet = new HashSet<int>();
            for (int i = 0; i < vertices.Count; i++)
            {
                XYZ vPos = vertices[i].Position;
                foreach (var d in data.DrainPoints)
                {
                    if (vPos.DistanceTo(d) < 0.5)  // tolerance
                    {
                        drainIndexSet.Add(i);
                        break;
                    }
                }
            }

            if (drainIndexSet.Count == 0)
            {
                data.Log(LogColorHelper.Red("No drain vertices matched any mesh vertices."));
                return;
            }

            data.Log(LogColorHelper.Green($"Drain vertices resolved: {drainIndexSet.Count}"));

            // -------------------------------------------------------
            // PROCESS + APPLY SLOPE
            // -------------------------------------------------------
            int processed = 0;
            int skipped = 0;

            double highestElevFt = 0;
            double longestPathFt = 0;

            Stopwatch sw = Stopwatch.StartNew();

            using (Transaction tx = new Transaction(doc, "Apply Slope Elevations"))
            {
                tx.Start();

                for (int i = 0; i < vertices.Count; i++)
                {
                    var vertex = vertices[i];
                    XYZ pos = vertex.Position;

                    double shortestDistFt = dijkstra.ComputeShortestPath(i, drainIndexSet);

                    if (double.IsInfinity(shortestDistFt) || shortestDistFt > thresholdFt)
                    {
                        skipped++;
                        continue;
                    }

                    double newElevationFt = shortestDistFt * slopeFactor;

                    try
                    {
                        editor.ModifySubElement(vertex, newElevationFt);
                        processed++;

                        double pathMeters =
                            UnitUtils.ConvertFromInternalUnits(shortestDistFt, UnitTypeId.Meters);

                        double elevMeters =
                            UnitUtils.ConvertFromInternalUnits(newElevationFt, UnitTypeId.Meters);

                        data.Log(
                            LogColorHelper.Black(
                                $"Vertex {i + 1}/{vertices.Count}: Path {pathMeters:0.000} m → Elev {elevMeters:0.000} m"
                            )
                        );

                        if (newElevationFt > highestElevFt) highestElevFt = newElevationFt;
                        if (shortestDistFt > longestPathFt) longestPathFt = shortestDistFt;
                    }
                    catch
                    {
                        skipped++;
                    }
                }

                tx.Commit();
            }

            sw.Stop();

            // -------------------------------------------------------
            // SUMMARY (meters)
            // -------------------------------------------------------
            double highestM = UnitUtils.ConvertFromInternalUnits(highestElevFt, UnitTypeId.Millimeters);
            double longestM = UnitUtils.ConvertFromInternalUnits(longestPathFt, UnitTypeId.Meters);

            data.Log(LogColorHelper.Green($"Vertices Processed: {processed}"));
            data.Log(LogColorHelper.Yellow($"Vertices Skipped: {skipped}"));
            data.Log(LogColorHelper.Black($"Highest Elevation: {highestM:0.000} m"));
            data.Log(LogColorHelper.Black($"Longest Path Distance: {longestM:0.000} m"));
            data.Log(LogColorHelper.Cyan($"Total Time: {sw.Elapsed.TotalSeconds:0.00} sec"));
            data.Log(LogColorHelper.Green("===== AutoSlope Finished Successfully ✔ ====="));

            // UPDATE VIEWMODEL SUMMARY
            data.Vm.VerticesProcessed = processed;
            data.Vm.VerticesSkipped = skipped;
            data.Vm.HighestElevation = highestM;
            data.Vm.LongestPathMeters = longestM;

            data.Vm.SummaryText =
            $@"====================== AutoSlope Summary ⚙️ ======================
✔️ Vertices Processed     : {processed}
⏸️ Vertices Skipped       : {skipped}
📈 Elevation (Highest)     : {highestM:0.000} m   ↑
🛣️ Longest Path Distance  : {longestM:0.000} m  →
⏱️ Total Time             : {sw.Elapsed.TotalSeconds:0.00} sec";

            // -------------------------------------------------------
            // OPTION A — WRITE SHARED PARAMETERS AT END
            // -------------------------------------------------------
            AutoSlopeParameterWriter.UpdateAllParameters(
                doc,
                roof,
                data,
                highestM,
                longestM,
                processed,
                skipped,
                sw.Elapsed.TotalSeconds,
                data.Log
            );
        }
    }
}
