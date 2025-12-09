using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Transaction = Autodesk.Revit.DB.Transaction;

namespace Revit26_Plugin.Commands
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class DijkstraPath2_2026 : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                UIApplication uiApp = commandData.Application;
                UIDocument uidoc = uiApp.ActiveUIDocument;
                Document doc = uidoc.Document;

                // Get slope percentage from user
                double slopePercentage = GetSlopeFromUser();
                if (slopePercentage < 0) return Result.Cancelled;

                // Select roof
                Reference roofRef = uidoc.Selection.PickObject(ObjectType.Element, new RoofFilter(), "Select a Roof.");
                RoofBase roof = doc.GetElement(roofRef) as RoofBase;
                if (roof == null)
                {
                    message = "Invalid roof selection.";
                    return Result.Failed;
                }

                // Get top face of roof
                Face topFace = GetTopFace(roof);
                if (topFace == null)
                {
                    message = "Could not find top face of the roof.";
                    return Result.Failed;
                }

                // Enable shape editing if needed
                SlabShapeEditor editor = roof.GetSlabShapeEditor();
                if (editor == null)
                {
                    TaskDialog.Show("SlabShapeEditor", "SlabShapeEditor is null — slab shape editing not supported for this roof.");
                    return Result.Failed;
                }

                if (!editor.IsEnabled)
                {
                    TaskDialog.Show("SlabShapeEditor", "SlabShapeEditor is NOT enabled. Enabling now…");
                    using (Transaction t = new Transaction(doc, "Enable slab shape editing"))
                    {
                        t.Start();
                        editor.Enable();
                        t.Commit();
                    }
                }
                else
                {
                    TaskDialog.Show("SlabShapeEditor", "SlabShapeEditor is already enabled.");
                }

                // Get all vertices
                List<SlabShapeVertex> allVertices = new List<SlabShapeVertex>();
                foreach (SlabShapeVertex v in editor.SlabShapeVertices)
                {
                    allVertices.Add(v);
                }

                if (allVertices.Count < 2)
                {
                    message = "Not enough vertices on roof.";
                    return Result.Failed;
                }

                // Filter vertices to only those on top face
                List<SlabShapeVertex> topVertices = allVertices;

                if (topVertices.Count == 0)
                {
                    message = "No vertices found on top face of roof.";
                    return Result.Failed;
                }

                // Ask user if they want to set vertices to zero or apply slopes
                TaskDialog slopeDialog = new TaskDialog("Roof Slope Options");
                slopeDialog.MainInstruction = "Select roof slope operation:";
                slopeDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Apply slopes with drainage paths");
                slopeDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Set all vertices to zero elevation");
                slopeDialog.CommonButtons = TaskDialogCommonButtons.Cancel;

                TaskDialogResult slopeResult = slopeDialog.Show();

                if (slopeResult == TaskDialogResult.Cancel)
                {
                    return Result.Cancelled;
                }
                else if (slopeResult == TaskDialogResult.CommandLink2)
                {
                    // Set all vertices to zero elevation
                    using (Transaction trans = new Transaction(doc, "Reset Roof Vertices to Zero"))
                    {
                        trans.Start();

                        int verticesReset = 0;
                        foreach (SlabShapeVertex vertex in topVertices)
                        {
                            if (vertex == null) continue;
                            editor.ModifySubElement(vertex, 0.0);
                            verticesReset++;
                        }

                        trans.Commit();

                        TaskDialog.Show("Success", $"Successfully reset {verticesReset} vertices to zero elevation!");
                        topFace = GetTopFace(roof);
                        if (topFace == null)
                        {
                            message = "Could not find top face of the roof.";
                            return Result.Failed;
                        }
                    }
                    return Result.Succeeded;
                }

                // Continue with normal slope application for CommandLink1

                // Select drain points
                List<SlabShapeVertex> drainPoints = PickDrainPoints(uidoc, topVertices);
                if (drainPoints.Count == 0)
                {
                    message = "No drain points selected.";
                    return Result.Failed;
                }

                // Build graph for pathfinding
                Dictionary<SlabShapeVertex, List<SlabShapeVertex>> graph = BuildGraph(topVertices, topFace);
                if (graph == null || graph.Count == 0)
                {
                    message = "Failed to build pathfinding graph.";
                    return Result.Failed;
                }

                // Precompute shortest paths from all vertices to their nearest drain points
                Dictionary<SlabShapeVertex, (SlabShapeVertex drain, double distance)> shortestPaths =
                    ComputeShortestPathsToDrains(graph, drainPoints);

                // Apply elevations based on shortest paths
                using (Transaction trans = new Transaction(doc, "Apply Roof Slopes"))
                {
                    trans.Start();

                    int verticesModified = 0;
                    foreach (SlabShapeVertex vertex in topVertices)
                    {
                        if (vertex == null || drainPoints.Contains(vertex)) continue;

                        if (shortestPaths.TryGetValue(vertex, out var pathInfo))
                        {
                            double elevationChange = (slopePercentage / 100.0) * pathInfo.distance;
                            double newElevation = elevationChange;
                            editor.ModifySubElement(vertex, newElevation);
                            verticesModified++;
                        }
                    }

                    trans.Commit();

                    TaskDialog.Show("Success", $"Roof slopes applied successfully!\nModified {verticesModified} vertices.");
                }

                return Result.Succeeded;
            }
            catch (OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = $"Exception: {ex.Message}\n{ex.StackTrace}";
                return Result.Failed;
            }
        }

        private Dictionary<SlabShapeVertex, (SlabShapeVertex drain, double distance)>
            ComputeShortestPathsToDrains(
                Dictionary<SlabShapeVertex, List<SlabShapeVertex>> graph,
                List<SlabShapeVertex> drainPoints)
        {
            var results = new Dictionary<SlabShapeVertex, (SlabShapeVertex, double)>();

            foreach (var vertex in graph.Keys)
            {
                if (vertex == null) continue;

                if (drainPoints.Contains(vertex))
                {
                    results[vertex] = (vertex, 0);
                    continue;
                }

                double minDistance = double.MaxValue;
                SlabShapeVertex nearestDrain = null;

                foreach (var drain in drainPoints)
                {
                    if (drain == null) continue;

                    List<XYZ> path = DijkstraPath(vertex, drain, graph);
                    if (path != null && path.Count >= 2)
                    {
                        double pathLength = 0;
                        for (int i = 0; i < path.Count - 1; i++)
                        {
                            if (path[i] != null && path[i + 1] != null)
                            {
                                pathLength += path[i].DistanceTo(path[i + 1]);
                            }
                        }

                        if (pathLength < minDistance)
                        {
                            minDistance = pathLength;
                            nearestDrain = drain;
                        }
                    }
                }

                if (nearestDrain != null)
                {
                    results[vertex] = (nearestDrain, minDistance);
                }
            }

            return results;
        }

        private List<XYZ> DijkstraPath(SlabShapeVertex start, SlabShapeVertex end,
            Dictionary<SlabShapeVertex, List<SlabShapeVertex>> graph)
        {
            try
            {
                if (start == null || end == null || graph == null) return null;

                var dist = new Dictionary<SlabShapeVertex, double>();
                var prev = new Dictionary<SlabShapeVertex, SlabShapeVertex>();
                var queue = new List<(double distance, SlabShapeVertex vertex)>();

                foreach (var v in graph.Keys)
                {
                    if (v == null) continue;
                    dist[v] = double.PositiveInfinity;
                    prev[v] = null;
                }

                dist[start] = 0;
                queue.Add((0, start));

                while (queue.Count > 0)
                {
                    queue.Sort((x, y) => x.distance.CompareTo(y.distance));
                    var (currentDist, current) = queue[0];
                    queue.RemoveAt(0);

                    if (current == end)
                        break;

                    if (!graph.ContainsKey(current)) continue;

                    foreach (var neighbor in graph[current])
                    {
                        if (neighbor == null || current.Position == null || neighbor.Position == null) continue;

                        double alt = dist[current] + current.Position.DistanceTo(neighbor.Position);
                        if (alt < (dist.ContainsKey(neighbor) ? dist[neighbor] : double.PositiveInfinity))
                        {
                            dist[neighbor] = alt;
                            prev[neighbor] = current;
                            queue.RemoveAll(x => x.vertex == neighbor);
                            queue.Add((alt, neighbor));
                        }
                    }
                }

                if (!prev.ContainsKey(end) || prev[end] == null) return null;

                var path = new List<XYZ>();
                for (var v = end; v != null; v = prev.ContainsKey(v) ? prev[v] : null)
                {
                    if (v.Position != null)
                        path.Insert(0, v.Position);
                }

                return path.Count > 0 ? path : null;
            }
            catch
            {
                return null;
            }
        }

        private double GetSlopeFromUser()
        {
            try
            {
                TaskDialog taskDialog = new TaskDialog("Slope Percentage");
                taskDialog.MainInstruction = "Select roof slope percentage:";
                taskDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "2% (Default)");
                taskDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "1% (Minimum)");
                taskDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "1.5% (Intermediate)");
                taskDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink4, "Custom value...");
                taskDialog.CommonButtons = TaskDialogCommonButtons.Cancel;

                TaskDialogResult result = taskDialog.Show();

                switch (result)
                {
                    case TaskDialogResult.CommandLink1: return 2.0;
                    case TaskDialogResult.CommandLink2: return 1.0;
                    case TaskDialogResult.CommandLink3: return 1.5;
                    case TaskDialogResult.CommandLink4: return GetCustomSlopeValue();
                    case TaskDialogResult.Cancel: return -1;
                    default: return -1;
                }
            }
            catch
            {
                return -1;
            }
        }


        private double GetCustomSlopeValue()
        {
            try
            {
                string input = Interaction.InputBox(
                    "Enter slope percentage (e.g., 1.5):",
                    "Custom Slope Value",
                    "2",
                    -1, -1);

                if (double.TryParse(input, out double value) && value > 0)
                {
                    return value;
                }
                return -1;
            }
            catch
            {
                return -1;
            }
        }

        private Face GetTopFace(RoofBase roof)
        {
            try
            {
                if (roof == null) return null;

                GeometryElement geomElem = roof.get_Geometry(new Options());
                Face topFace = null;
                double maxZ = double.MinValue;

                foreach (GeometryObject geomObj in geomElem)
                {
                    if (geomObj is Solid solid && solid != null)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            if (face == null) continue;

                            BoundingBoxUV bb = face.GetBoundingBox();
                            if (bb == null) continue;

                            UV midpointUV = new UV((bb.Min.U + bb.Max.U) / 2, (bb.Min.V + bb.Max.V) / 2);
                            XYZ midpoint = face.Evaluate(midpointUV);

                            if (midpoint != null && midpoint.Z > maxZ)
                            {
                                maxZ = midpoint.Z;
                                topFace = face;
                            }
                        }
                    }
                }
                return topFace;
            }
            catch
            {
                return null;
            }
        }

        private bool IsPointOnFace(XYZ point, Face face)
        {
            try
            {
                if (point == null || face == null) return false;

                IntersectionResult result = face.Project(point);
                if (result == null) return false;

                UV uv = result.UVPoint;
                BoundingBoxUV bb = face.GetBoundingBox();
                if (bb == null || bb.Min == null || bb.Max == null) return false;

                return bb.Min.U <= uv.U && uv.U <= bb.Max.U &&
                       bb.Min.V <= uv.V && uv.V <= bb.Max.V;
            }
            catch
            {
                return false;
            }
        }

        private List<SlabShapeVertex> PickDrainPoints(UIDocument uidoc, List<SlabShapeVertex> vertices)
        {
            List<SlabShapeVertex> drainPoints = new List<SlabShapeVertex>();
            try
            {
                if (uidoc == null || vertices == null || vertices.Count == 0)
                {
                    TaskDialog.Show("Error", "No vertices available for selection.");
                    return drainPoints;
                }

                // First, let the user know what to do
                TaskDialog.Show("Info",
                    "Please select drain points on the roof:\n\n" +
                    "1. Zoom in close to the roof\n" +
                    "2. Click on points where you want drains\n" +
                    "3. Press ESC when finished\n\n" +
                    "Make sure to click directly on the roof surface.");

                // Create a selection filter
                ISelectionFilter roofFilter = new RoofSurfaceFilter();

                // Let user pick multiple points
                IList<Reference> selectedRefs;
                try
                {
                    selectedRefs = uidoc.Selection.PickObjects(
                        ObjectType.PointOnElement,
                        roofFilter,
                        "Select drain points on roof surface (ESC to finish)");
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    // User pressed ESC - normal behavior
                    TaskDialog.Show("Info", "Selection cancelled by user.");
                    return drainPoints;
                }

                if (selectedRefs == null || selectedRefs.Count == 0)
                {
                    TaskDialog.Show("Warning", "No points were selected. Please try again.");
                    return drainPoints;
                }

                TaskDialog.Show("Info", $"Selected {selectedRefs.Count} points. Finding closest vertices...");

                // Match selected points to vertices
                foreach (Reference pointRef in selectedRefs)
                {
                    if (pointRef == null) continue;

                    try
                    {
                        XYZ selectedPoint = pointRef.GlobalPoint;

                        // Find the closest vertex within 2000 feet (reasonable tolerance)
                        double tolerance = 200;
                        SlabShapeVertex closestVertex = null;
                        double minDistance = double.MaxValue;

                        foreach (var vertex in vertices)
                        {
                            if (vertex == null || vertex.Position == null) continue;

                            double distance = vertex.Position.DistanceTo(selectedPoint);
                            if (distance < tolerance && distance < minDistance)
                            {
                                minDistance = distance;
                                closestVertex = vertex;
                            }
                        }

                        if (closestVertex != null)
                        {
                            if (!drainPoints.Contains(closestVertex))
                            {
                                drainPoints.Add(closestVertex);
                                TaskDialog.Show("Info", $"Added drain point at vertex position: {closestVertex.Position}");
                            }
                        }
                        else
                        {
                            TaskDialog.Show("Warning", $"Could not find a vertex within {tolerance} feet of selected point.");
                        }
                    }
                    catch (Exception ex)
                    {
                        TaskDialog.Show("Error", $"Error processing point: {ex.Message}");
                    }
                }

                if (drainPoints.Count == 0)
                {
                    TaskDialog.Show("Warning",
                        "No valid drain points were found.\n" +
                        "Please ensure:\n" +
                        "1. You're clicking directly on the roof surface\n" +
                        "2. You're clicking near existing vertices\n" +
                        "3. Try selecting fewer points at key locations");
                }
                else
                {
                    TaskDialog.Show("Success", $"Successfully selected {drainPoints.Count} drain point(s).");
                }

                return drainPoints;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Error in PickDrainPoints: {ex.Message}\n{ex.StackTrace}");
                return drainPoints;
            }
        }

        private Dictionary<SlabShapeVertex, List<SlabShapeVertex>> BuildGraph(List<SlabShapeVertex> vertices, Face topFace)
        {
            Dictionary<SlabShapeVertex, List<SlabShapeVertex>> graph = new Dictionary<SlabShapeVertex, List<SlabShapeVertex>>();
            try
            {
                if (vertices == null || topFace == null) return graph;

                // Convert 200 meters to feet (Revit 2026 uses feet internally)
                // 200 meters ≈ 656.168 feet
                double threshold = 200.0 * 3.28084;

                foreach (var v in vertices)
                {
                    if (v == null || v.Position == null) continue;
                    graph[v] = new List<SlabShapeVertex>();

                    foreach (var other in vertices)
                    {
                        if (other == null || other.Position == null || v == other) continue;
                        if (v.Position.DistanceTo(other.Position) > threshold) continue;

                        if (IsValidConnection(v.Position, other.Position, topFace))
                            graph[v].Add(other);
                    }
                }
                return graph;
            }
            catch
            {
                return graph;
            }
        }

        private bool IsValidConnection(XYZ start, XYZ end, Face face)
        {
            try
            {
                if (start == null || end == null || face == null) return false;

                Line line = Line.CreateBound(start, end);
                if (line == null) return false;

                // Test 5 points along the line
                for (double t = 0.2; t < 1.0; t += 0.2)
                {
                    XYZ testPoint = line.Evaluate(t, true);
                    if (!IsPointOnFace(testPoint, face))
                        return false;
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private class RoofFilter : ISelectionFilter
        {
            public bool AllowElement(Element e) => e is RoofBase;
            public bool AllowReference(Reference r, XYZ p) => false;
        }

        private class RoofSurfaceFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                // Allow selection on any element
                return true;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                // Only allow references that are points on faces
                // This ensures we're clicking on surfaces, not edges
                return true;
            }
        }
    }
}