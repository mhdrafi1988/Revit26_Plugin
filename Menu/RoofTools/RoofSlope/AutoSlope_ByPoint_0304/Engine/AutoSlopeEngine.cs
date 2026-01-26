// =======================================================
// File: AutoSlopeEngine.cs
// Purpose: Core AutoSlope execution + metrics aggregation
// Revit: 2026
// =======================================================

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
        public static void Execute(UIApplication app, AutoSlopePayload data)
        {
            Document doc = app.ActiveUIDocument.Document;
            RoofBase roof = doc.GetElement(data.RoofId) as RoofBase;
            if (roof == null) return;

            SlabShapeEditor editor = roof.GetSlabShapeEditor();

            // --------------------------------------------------
            // RESET VERTICES
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
                UnitUtils.ConvertToInternalUnits(
                    data.ThresholdMeters,
                    UnitTypeId.Meters);

            Face topFace = AutoSlopeGeometry.GetTopFace(roof);
            if (topFace == null)
            {
                data.Log?.Invoke(LogColorHelper.Red("Top face not found. Aborting."));
                return;
            }

            var dijkstra =
                new DijkstraPathEngine(vertices, topFace, thresholdFt);

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

            double maxElevFt = 0.0;
            double maxPathFt = 0.0;
            double sumElevFt = 0.0;

            Stopwatch sw = Stopwatch.StartNew();

            using (Transaction tx = new Transaction(doc, "Apply AutoSlope"))
            {
                tx.Start();

                for (int i = 0; i < vertices.Count; i++)
                {
                    double pathFt =
                        dijkstra.ComputeShortestPath(i, drainIndices);

                    if (double.IsInfinity(pathFt) || pathFt > thresholdFt)
                    {
                        skipped++;
                        continue;
                    }

                    double elevFt = pathFt * slopeFactor;
                    editor.ModifySubElement(vertices[i], elevFt);

                    sumElevFt += elevFt;
                    processed++;

                    if (elevFt > maxElevFt) maxElevFt = elevFt;
                    if (pathFt > maxPathFt) maxPathFt = pathFt;
                }

                tx.Commit();
            }

            sw.Stop();

            // --------------------------------------------------
            // METRICS
            // --------------------------------------------------
            int highest_mm =
                (int)Math.Round(
                    UnitUtils.ConvertFromInternalUnits(
                        maxElevFt,
                        UnitTypeId.Millimeters),
                    MidpointRounding.AwayFromZero);

            double avgElevFt =
                processed > 0 ? sumElevFt / processed : 0.0;

            double longest_ft = maxPathFt;

            double longest_m =
                Math.Round(
                    UnitUtils.ConvertFromInternalUnits(
                        maxPathFt,
                        UnitTypeId.Meters),
                    2,
                    MidpointRounding.AwayFromZero);

            int durationSec =
                (int)Math.Round(sw.Elapsed.TotalSeconds);

            string runDate =
                DateTime.Now.ToString("dd-MM-yy HH:mm");

            // --------------------------------------------------
            // UPDATE VIEWMODEL  ? FIXES SummaryText
            // --------------------------------------------------
            if (data?.Vm != null)
            {
                data.Vm.VerticesProcessed = processed;
                data.Vm.VerticesSkipped = skipped;
                data.Vm.DrainCount = data.DrainPoints.Count;
                data.Vm.HighestElevation_mm = highest_mm;
                data.Vm.AverageElevation_ft = avgElevFt;
                data.Vm.LongestPath_m = longest_m;
                data.Vm.RunDuration_sec = durationSec;
                data.Vm.RunDate = runDate;
            }

            // --------------------------------------------------
            // WRITE PARAMETERS
            // --------------------------------------------------
            AutoSlopeParameterWriter.WriteAll(
                doc,
                roof,
                data,
                highest_mm,
                avgElevFt,
                longest_ft,
                processed,
                skipped,
                durationSec);

            data.Log?.Invoke(LogColorHelper.Green("AutoSlope completed successfully."));
        }
    }
}
