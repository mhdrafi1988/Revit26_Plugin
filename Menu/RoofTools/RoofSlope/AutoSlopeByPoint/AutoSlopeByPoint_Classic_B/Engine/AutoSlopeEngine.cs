using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Revit26_Plugin.AutoSlopeByPoint_WIP.Helpers;
using Revit26_Plugin.AutoSlopeByPoint_WIP.Models;
using Revit26_Plugin.AutoSlopeByPoint_WIP.Parameters;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace Revit26_Plugin.AutoSlopeByPoint_WIP.Engine
{
    public static class AutoSlopeEngine
    {
        public static void Execute(UIApplication app, AutoSlopePayload data)
        {
            Document doc = app.ActiveUIDocument.Document;
            RoofBase roof = doc.GetElement(data.RoofId) as RoofBase;
            if (roof == null) return;

            SlabShapeEditor editor = roof.GetSlabShapeEditor();

            // --------------------------------------------------
            // RESET VERTICES (shape editing already enabled in Command)
            // --------------------------------------------------
            using (Transaction tx = new Transaction(doc, "Reset Roof Vertices"))
            {
                tx.Start();
                foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                    editor.ModifySubElement(v, 0);
                tx.Commit();
            }

            List<SlabShapeVertex> vertices = new();
            foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                vertices.Add(v);

            double slopeFactor = data.SlopePercent / 100.0;
            double thresholdFt =
                UnitUtils.ConvertToInternalUnits(data.ThresholdMeters, UnitTypeId.Meters);

            Face topFace = AutoSlopeGeometry.GetTopFace(roof);
            if (topFace == null)
            {
                data.Log(LogColorHelper.Red("Top face not found. Aborting."));
                return;
            }

            var dijkstra = new DijkstraPathEngine(vertices, topFace, thresholdFt);

            HashSet<int> drainIndices = new();
            for (int i = 0; i < vertices.Count; i++)
            {
                foreach (XYZ d in data.DrainPoints)
                {
                    if (vertices[i].Position.DistanceTo(d) < 0.5)
                    {
                        drainIndices.Add(i);
                        break;
                    }
                }
            }

            int processed = 0;
            int skipped = 0;
            double maxElevFt = 0;
            double maxPathFt = 0;

            Stopwatch sw = Stopwatch.StartNew();

            using (Transaction tx = new Transaction(doc, "Apply AutoSlope"))
            {
                tx.Start();

                for (int i = 0; i < vertices.Count; i++)
                {
                    double pathFt = dijkstra.ComputeShortestPath(i, drainIndices);

                    if (double.IsInfinity(pathFt) || pathFt > thresholdFt)
                    {
                        skipped++;
                        continue;
                    }

                    double elevFt = pathFt * slopeFactor;
                    editor.ModifySubElement(vertices[i], elevFt);

                    processed++;
                    if (elevFt > maxElevFt) maxElevFt = elevFt;
                    if (pathFt > maxPathFt) maxPathFt = pathFt;
                }

                tx.Commit();
            }

            sw.Stop();

            // ==================================================
            // ?? UNIT SPLIT (THIS IS THE KEY CHANGE)
            // ==================================================

            // Highest elevation ? MILLIMETERS
            int highest_mm =(int)Math.Round(UnitUtils.ConvertFromInternalUnits(maxElevFt, UnitTypeId.Millimeters), MidpointRounding.AwayFromZero);

            // Longest path ? METERS (0.00)
            double longest_m =Math.Round(UnitUtils.ConvertFromInternalUnits(maxPathFt, UnitTypeId.Meters),2,MidpointRounding.AwayFromZero);
            double longest_ft = maxPathFt;



            int durationSec = (int)Math.Round(sw.Elapsed.TotalSeconds);
            string runDate = DateTime.Now.ToString("dd-MM-yy HH:mm");

            // --------------------------------------------------
            // UPDATE VIEWMODEL (UI)
            // --------------------------------------------------
            data.Vm.VerticesProcessed = processed;
            data.Vm.VerticesSkipped = skipped;
            data.Vm.HighestElevation_mm = highest_mm;
            data.Vm.LongestPath_m = maxPathFt;
            data.Vm.DrainCount = data.DrainPoints.Count;
            data.Vm.RunDuration_sec = durationSec;
            data.Vm.RunDate = runDate;

            // --------------------------------------------------
            // WRITE PARAMETERS TO ROOF
            // --------------------------------------------------
            ILogService.WriteAll(
                doc,
                roof,
                data,
                highest_mm,
                longest_ft,
                processed,
                skipped,
                durationSec);

            // --------------------------------------------------
            // UI LOG
            // --------------------------------------------------
            data.Log(LogColorHelper.Cyan("===== AutoSlope Summary ====="));
            data.Log(LogColorHelper.Green($"Vertices Processed : {processed}"));
            data.Log(LogColorHelper.Yellow($"Vertices Skipped   : {skipped}"));
            data.Log(LogColorHelper.Cyan($"Highest Elevation  : {highest_mm:0} mm"));
            data.Log(LogColorHelper.Cyan($"Longest Path       : {longest_m:0.00} m"));
            data.Log(LogColorHelper.Cyan($"Drain Count        : {data.DrainPoints.Count}"));
            data.Log(LogColorHelper.Cyan($"Run Duration       : {durationSec} sec"));
            data.Log(LogColorHelper.Cyan($"Run Date           : {runDate}"));
            data.Log(LogColorHelper.Green("===== AutoSlope Finished ? ====="));
        }
    }
}
