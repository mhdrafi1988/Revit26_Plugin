using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using AutoSlopeByPointTwoSlopes_01_00.Core.Models;
using AutoSlopeByPointTwoSlopes_01_00.Core.Parameters;
using AutoSlopeByPointTwoSlopes_01_00.Infrastructure.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace AutoSlopeByPointTwoSlopes_01_00.Core.Engine
{
    public static class AutoSlopeEngine
    {
        public static void Execute(UIApplication app, AutoSlopePayload data)
        {
            Document doc = app.ActiveUIDocument.Document;
            RoofBase roof = doc.GetElement(data.RoofId) as RoofBase;
            if (roof == null)
            {
                data.Log(LogColorHelper.Red("Roof not found. Aborting."));
                return;
            }

            SlabShapeEditor editor = roof.GetSlabShapeEditor();
            if (editor == null || !editor.IsValidObject)
            {
                data.Log(LogColorHelper.Red("Roof slab shape editor is not available. Aborting."));
                return;
            }

            // CRITICAL FIX: Ensure shape editing is enabled with proper transaction
            using (Transaction enableTx = new Transaction(doc, "AutoSlope - Enable Shape Editing"))
            {
                enableTx.Start();
                try
                {
                    if (!editor.IsEnabled)
                    {
                        editor.Enable();
                        data.Log(LogColorHelper.Cyan("Shape editing enabled for roof."));
                    }
                    enableTx.Commit();
                }
                catch (Exception ex)
                {
                    data.Log(LogColorHelper.Red($"Failed to enable shape editing: {ex.Message}"));
                    enableTx.RollBack();
                    return;
                }
            }

            // Reset vertices to zero with MAIN TRANSACTION
            using (Transaction resetTx = new Transaction(doc, "AutoSlope - Reset Roof Vertices"))
            {
                resetTx.Start();
                try
                {
                    // Get fresh editor reference after enabling
                    editor = roof.GetSlabShapeEditor();
                    if (editor == null || !editor.IsValidObject)
                    {
                        data.Log(LogColorHelper.Red("Shape editor not available after enable."));
                        resetTx.RollBack();
                        return;
                    }

                    foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                    {
                        if (v != null && v.IsValidObject)
                        {
                            editor.ModifySubElement(v, 0);
                        }
                    }
                    resetTx.Commit();
                    data.Log(LogColorHelper.Green("Reset all roof vertices to zero elevation."));
                }
                catch (Exception ex)
                {
                    data.Log(LogColorHelper.Red($"Failed to reset vertices: {ex.Message}"));
                    resetTx.RollBack();
                    return;
                }
            }

            // ✅ Get fresh vertices list after reset - NO TRANSACTION NEEDED FOR READING
            List<SlabShapeVertex> vertices = new List<SlabShapeVertex>();
            try
            {
                // Get fresh editor reference after reset
                editor = roof.GetSlabShapeEditor();
                if (editor == null || !editor.IsValidObject)
                {
                    data.Log(LogColorHelper.Red("Shape editor not available for reading vertices."));
                    return;
                }

                for (int i = 0; i < editor.SlabShapeVertices.Size; i++)
                {
                    SlabShapeVertex vertex = editor.SlabShapeVertices.get_Item(i);
                    if (vertex != null && vertex.IsValidObject)
                    {
                        vertices.Add(vertex);
                    }
                }
                data.Log(LogColorHelper.Cyan($"Found {vertices.Count} vertices on roof."));
            }
            catch (Exception ex)
            {
                data.Log(LogColorHelper.Red($"Failed to read vertices: {ex.Message}"));
                return;
            }

            if (vertices.Count == 0)
            {
                data.Log(LogColorHelper.Red("No vertices found on roof. Aborting."));
                return;
            }

            double thresholdFt = UnitUtils.ConvertToInternalUnits(data.ThresholdMeters, UnitTypeId.Meters);

            Face topFace = AutoSlopeGeometry.GetTopFace(roof);
            if (topFace == null)
            {
                data.Log(LogColorHelper.Red("Top face not found. Aborting."));
                return;
            }

            // Build final drain points from user picks + nearby roof shape points.
            List<XYZ> finalDrainPoints = data.DrainPoints ?? new List<XYZ>();

            if (data.EnableDrainTolerance && data.DrainToleranceMm > 0)
            {
                data.Log(LogColorHelper.Cyan(
                    $"🔍 Checking for nearby roof shape points within {data.DrainToleranceMm}mm of selected points..."));

                finalDrainPoints = DrainDetectionHelper.DetectDrainsWithinRadius(
                    roof,
                    finalDrainPoints,
                    data.DrainToleranceMm,
                    data.Log);

                finalDrainPoints = DrainDetectionHelper.RemoveDuplicates(
                    finalDrainPoints,
                    data.DrainToleranceMm);
            }

            if (finalDrainPoints == null || finalDrainPoints.Count == 0)
            {
                data.Log(LogColorHelper.Red("No drain points are available. Aborting."));
                return;
            }

            data.Log(LogColorHelper.Cyan($"Total drain points: {finalDrainPoints.Count}"));

            var dijkstra = new DijkstraPathEngine(vertices, topFace, thresholdFt);

            double drainMatchToleranceFt = data.EnableDrainTolerance && data.DrainToleranceMm > 0
                ? UnitUtils.ConvertToInternalUnits(data.DrainToleranceMm, UnitTypeId.Millimeters)
                : 0.001;

            HashSet<int> drainIndices = new HashSet<int>();
            for (int i = 0; i < vertices.Count; i++)
            {
                if (vertices[i] == null || !vertices[i].IsValidObject)
                    continue;

                foreach (XYZ drainPoint in finalDrainPoints)
                {
                    if (drainPoint == null)
                        continue;

                    if (vertices[i].Position.DistanceTo(drainPoint) <= drainMatchToleranceFt)
                    {
                        drainIndices.Add(i);
                        break;
                    }
                }
            }

            if (drainIndices.Count == 0)
            {
                data.Log(LogColorHelper.Red("No roof vertices matched the selected drain points. Aborting."));
                return;
            }

            data.Log(LogColorHelper.Cyan($"Found {drainIndices.Count} drain vertices."));

            int processed = 0;
            int skipped = 0;
            double maxElevFt = 0;
            double maxPathFt = 0;
            List<VertexData> vertexDataList = new List<VertexData>();
            Stopwatch sw = Stopwatch.StartNew();

            // MAIN TRANSACTION for slope modifications
            using (Transaction slopeTx = new Transaction(doc, "AutoSlope - Apply Slopes"))
            {
                slopeTx.Start();
                try
                {
                    // Get fresh editor reference
                    editor = roof.GetSlabShapeEditor();
                    if (editor == null || !editor.IsValidObject)
                    {
                        data.Log(LogColorHelper.Red("Shape editor not available for slope application."));
                        slopeTx.RollBack();
                        return;
                    }

                    // Refresh vertices list within transaction
                    List<SlabShapeVertex> currentVertices = new List<SlabShapeVertex>();
                    for (int i = 0; i < editor.SlabShapeVertices.Size; i++)
                    {
                        SlabShapeVertex vertex = editor.SlabShapeVertices.get_Item(i);
                        if (vertex != null && vertex.IsValidObject)
                        {
                            currentVertices.Add(vertex);
                        }
                    }

                    if (currentVertices.Count != vertices.Count)
                    {
                        data.Log(LogColorHelper.Yellow($"Warning: Vertex count changed from {vertices.Count} to {currentVertices.Count}. Using current vertices."));
                        vertices = currentVertices;
                    }

                    for (int i = 0; i < vertices.Count; i++)
                    {
                        if (vertices[i] == null || !vertices[i].IsValidObject)
                        {
                            skipped++;
                            continue;
                        }

                        double pathFt = dijkstra.ComputeShortestPath(i, drainIndices);

                        if (double.IsInfinity(pathFt) || pathFt > thresholdFt)
                        {
                            skipped++;
                            vertexDataList.Add(new VertexData
                            {
                                VertexIndex = i,
                                Position = vertices[i].Position,
                                PathLengthMeters = double.IsInfinity(pathFt) ? 0 : UnitUtils.ConvertFromInternalUnits(pathFt, UnitTypeId.Meters),
                                ElevationOffsetMm = 0,
                                NearestDrainIndex = -1,
                                DirectionVector = XYZ.Zero,
                                WasProcessed = false,
                                AppliedSlopePercent = 0,
                                IsSpecialVertex = data.SelectedVertexIndices?.Contains(i) ?? false
                            });
                            continue;
                        }

                        // Determine which slope to use
                        double slopePercent;
                        bool isSpecial = data.SelectedVertexIndices != null && data.SelectedVertexIndices.Contains(i);

                        if (isSpecial)
                        {
                            slopePercent = data.SpecialSlopePercent;
                        }
                        else
                        {
                            slopePercent = data.RemainingSlopePercent;
                        }

                        double slopeFactor = slopePercent / 100.0;
                        double elevFt = pathFt * slopeFactor;

                        // Apply the elevation modification
                        editor.ModifySubElement(vertices[i], elevFt);

                        // Store slope mapping
                        data.VertexSlopeMapping[i] = slopePercent;

                        processed++;
                        if (elevFt > maxElevFt)
                            maxElevFt = elevFt;

                        if (pathFt > maxPathFt)
                            maxPathFt = pathFt;

                        int nearestDrainIndex = FindNearestDrainIndex(vertices[i].Position, finalDrainPoints);
                        XYZ directionVector = nearestDrainIndex >= 0
                            ? CalculateDirectionVector(vertices[i].Position, finalDrainPoints[nearestDrainIndex])
                            : XYZ.Zero;

                        vertexDataList.Add(new VertexData
                        {
                            VertexIndex = i,
                            Position = vertices[i].Position,
                            PathLengthMeters = UnitUtils.ConvertFromInternalUnits(pathFt, UnitTypeId.Meters),
                            ElevationOffsetMm = UnitUtils.ConvertFromInternalUnits(elevFt, UnitTypeId.Millimeters),
                            NearestDrainIndex = nearestDrainIndex,
                            DirectionVector = directionVector,
                            WasProcessed = true,
                            AppliedSlopePercent = slopePercent,
                            IsSpecialVertex = isSpecial
                        });
                    }

                    slopeTx.Commit();
                    data.Log(LogColorHelper.Green($"Successfully applied slopes to {processed} vertices."));
                }
                catch (Exception ex)
                {
                    data.Log(LogColorHelper.Red($"Failed to apply slopes: {ex.Message}"));
                    slopeTx.RollBack();
                    return;
                }
            }

            // Create red detail circles for selected vertices (separate transaction)
            if (data.SelectedVertexIndices != null && data.SelectedVertexIndices.Count > 0)
            {
                CreateDetailCircles(doc, vertices, data.SelectedVertexIndices, data);
            }

            sw.Stop();

            int highest_mm = (int)Math.Round(
                UnitUtils.ConvertFromInternalUnits(maxElevFt, UnitTypeId.Millimeters),
                MidpointRounding.AwayFromZero);

            double longest_m = Math.Round(
                UnitUtils.ConvertFromInternalUnits(maxPathFt, UnitTypeId.Meters),
                2,
                MidpointRounding.AwayFromZero);

            int durationSec = (int)Math.Round(sw.Elapsed.TotalSeconds);
            string runDate = DateTime.Now.ToString("dd-MM-yy HH:mm");

            data.Vm.VerticesProcessed = processed;
            data.Vm.VerticesSkipped = skipped;
            data.Vm.HighestElevation_mm = highest_mm;
            data.Vm.LongestPath_m = longest_m;
            data.Vm.DrainCount = finalDrainPoints.Count;
            data.Vm.RunDuration_sec = durationSec;
            data.Vm.RunDate = runDate;

            // Write parameters to roof element
            AutoSlopeParameterWriter.WriteAll(
                doc,
                roof,
                data,
                highest_mm,
                maxPathFt,
                processed,
                skipped,
                durationSec,
                finalDrainPoints.Count,
                "P.04.01");

            // Export to Excel if enabled
            if (data.ExportConfig?.ExportToExcel == true)
            {
                string compactPath = ExcelExportHelper.ExportCompactVertexData(
                    data,
                    vertexDataList,
                    roof,
                    data.RemainingSlopePercent);

                if (!string.IsNullOrEmpty(compactPath))
                {
                    data.Log(LogColorHelper.Green($"✅ Compact Excel data exported to: {compactPath}"));
                }

                if (data.ExportConfig.IncludeVertexDetails)
                {
                    string detailedPath = ExcelExportHelper.ExportDetailedVertexData(
                        data,
                        vertexDataList,
                        roof,
                        finalDrainPoints,
                        data.RemainingSlopePercent);

                    if (!string.IsNullOrEmpty(detailedPath))
                    {
                        data.Log(LogColorHelper.Green($"✅ Detailed Excel data exported to: {detailedPath}"));
                    }
                }
            }

            // Log summary with multi-slope information
            data.Log(LogColorHelper.Cyan("===== AutoSlope Summary ====="));
            data.Log(LogColorHelper.Green($"Special Vertices Slope    : {data.SpecialSlopePercent}%"));
            data.Log(LogColorHelper.Green($"Remaining Vertices Slope  : {data.RemainingSlopePercent}%"));
            data.Log(LogColorHelper.Green($"Special Vertices Count    : {data.SelectedVertexIndices?.Count ?? 0}"));
            data.Log(LogColorHelper.Green($"Vertices Processed        : {processed}"));
            data.Log(LogColorHelper.Yellow($"Vertices Skipped          : {skipped}"));
            data.Log(LogColorHelper.Cyan($"Highest Elevation         : {highest_mm:0} mm"));
            data.Log(LogColorHelper.Cyan($"Longest Path              : {longest_m:0.00} m"));
            data.Log(LogColorHelper.Cyan($"Drain Count               : {finalDrainPoints.Count}"));
            data.Log(LogColorHelper.Cyan($"Run Duration              : {durationSec} sec"));
            data.Log(LogColorHelper.Green("===== AutoSlope Finished Successfully ====="));
        }

        private static void CreateDetailCircles(Document doc, List<SlabShapeVertex> vertices, HashSet<int> selectedIndices, AutoSlopePayload data)
        {
            data.Log(LogColorHelper.Cyan($"Creating red detail circles for {selectedIndices.Count} special vertices..."));

            View activeView = doc.ActiveView;

            using (Transaction tx = new Transaction(doc, "Create Detail Circles for Special Vertices"))
            {
                tx.Start();

                double radiusFt = UnitUtils.ConvertToInternalUnits(500, UnitTypeId.Millimeters); // 500mm radius
                int circlesCreated = 0;

                foreach (int index in selectedIndices)
                {
                    if (index < 0 || index >= vertices.Count)
                        continue;

                    SlabShapeVertex vertex = vertices[index];
                    if (vertex == null || !vertex.IsValidObject)
                        continue;

                    XYZ vertexPos = vertex.Position;
                    XYZ center = new XYZ(vertexPos.X, vertexPos.Y, vertexPos.Z);

                    try
                    {
                        // Create a circle using ModelCurve
                        Plane plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, center);
                        SketchPlane sketchPlane = SketchPlane.Create(doc, plane);

                        Arc circle = Arc.Create(plane, radiusFt, 0, 2 * Math.PI);
                        ModelCurve modelCurve = doc.Create.NewModelCurve(circle, sketchPlane);

                        // Set line style to red
                        GraphicsStyle redLineStyle = GetOrCreateRedLineStyle(doc);
                        if (redLineStyle != null)
                        {
                            modelCurve.LineStyle = redLineStyle;
                        }

                        circlesCreated++;
                    }
                    catch (Exception ex)
                    {
                        data.Log(LogColorHelper.Yellow($"Warning: Could not create detail circle at vertex {index}: {ex.Message}"));
                    }
                }

                tx.Commit();
                data.Log(LogColorHelper.Green($"✅ Created {circlesCreated} red detail circles for special vertices."));
            }
        }

        private static GraphicsStyle GetOrCreateRedLineStyle(Document doc)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            var graphicsStyles = collector.OfClass(typeof(GraphicsStyle)).Cast<GraphicsStyle>().ToList();

            foreach (var style in graphicsStyles)
            {
                if (style.Name != null && style.Name.ToLower().Contains("red"))
                {
                    return style;
                }
            }

            return graphicsStyles.FirstOrDefault();
        }

        private static int FindNearestDrainIndex(XYZ vertexPos, List<XYZ> drainPoints)
        {
            if (drainPoints == null || drainPoints.Count == 0)
                return -1;

            int nearestIndex = 0;
            double minDistance = double.MaxValue;

            for (int i = 0; i < drainPoints.Count; i++)
            {
                if (drainPoints[i] == null)
                    continue;

                double distance = vertexPos.DistanceTo(drainPoints[i]);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestIndex = i;
                }
            }

            return nearestIndex;
        }

        private static XYZ CalculateDirectionVector(XYZ fromPoint, XYZ toPoint)
        {
            if (fromPoint.DistanceTo(toPoint) < 0.001)
                return XYZ.Zero;

            XYZ vector = toPoint - fromPoint;
            return vector.Normalize();
        }
    }
}