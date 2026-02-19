using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Linq;
using Transaction = Autodesk.Revit.DB.Transaction;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Windows; // For TaskDialog

// v10-12 (simplified initializations + adaptive threshold)
namespace Revit26_Plugin.Commands
{
    [Autodesk.Revit.Attributes.Transaction(Autodesk.Revit.Attributes.TransactionMode.Manual)]
    [Autodesk.Revit.Attributes.Regeneration(Autodesk.Revit.Attributes.RegenerationOption.Manual)]
    public class RoofSloperClassic1_v2 : IExternalCommand
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

                // Enable shape editing if needed
                if (!roof.GetSlabShapeEditor().IsEnabled)
                {
                    using (var tx = new Transaction(doc, "Enable Slab Editing"))
                    {
                        tx.Start();
                        roof.GetSlabShapeEditor().Enable();
                        tx.Commit();
                    }
                }

                // Reset all vertices to zero elevation
                using (var resetTx = new Transaction(doc, "Reset Vertices to Zero"))
                {
                    resetTx.Start();
                    foreach (SlabShapeVertex vertex in roof.GetSlabShapeEditor().SlabShapeVertices)
                        roof.GetSlabShapeEditor().ModifySubElement(vertex, 0);
                    resetTx.Commit();
                }

                // Top face
                Face topFace = GetTopFace(roof);
                if (topFace == null)
                {
                    message = "Could not find top face of the roof.";
                    return Result.Failed;
                }

                // All vertices -> list
                var allVertices = roof.GetSlabShapeEditor().SlabShapeVertices
                    .Cast<SlabShapeVertex>()
                    .ToList();

                if (allVertices.Count < 2)
                {
                    message = "Not enough vertices on roof.";
                    return Result.Failed;
                }

                // Only vertices that sit on the top face
                var topVertices = allVertices
                    .Where(v => IsPointOnFace(v.Position, topFace))
                    .ToList();

                // Pick drains
                var drainPoints = PickDrainPoints(uidoc, topVertices);
                if (drainPoints.Count == 0)
                {
                    message = "No drain points selected.";
                    return Result.Cancelled;
                }

                // Build graph (ADAPTIVE threshold)
                var graph = BuildGraph(topVertices, topFace);
                if (graph.Count == 0)
                {
                    message = "Failed to build pathfinding graph.";
                    return Result.Failed;
                }

                // Shortest paths to nearest drain
                var shortestPaths = ComputeShortestPathsToDrains(graph, drainPoints);

                // Apply elevations
                using (var trans = new Transaction(doc, "Apply Roof Slopes"))
                {
                    trans.Start();

                    foreach (var vertex in topVertices)
                    {
                        if (vertex == null || drainPoints.Contains(vertex)) continue;

                        (SlabShapeVertex drain, double distance) pathInfo;
                        if (shortestPaths.TryGetValue(vertex, out pathInfo))
                        {
                            double elevationChange = (slopePercentage / 100.0) * pathInfo.distance;
                            roof.GetSlabShapeEditor().ModifySubElement(vertex, elevationChange);
                        }
                    }

                    trans.Commit();
                }

                return Result.Succeeded;
            }
            catch (OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = "Exception: " + ex.Message;
                return Result.Failed;
            }
        }

        private Dictionary<SlabShapeVertex, (SlabShapeVertex drain, double distance)> ComputeShortestPathsToDrains(
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

                    var path = DijkstraPath(vertex, drain, graph);
                    if (path != null && path.Count >= 2)
                    {
                        double pathLength = 0.0;
                        for (int i = 0; i < path.Count - 1; i++)
                            pathLength += path[i].DistanceTo(path[i + 1]);

                        if (pathLength < minDistance)
                        {
                            minDistance = pathLength;
                            nearestDrain = drain;
                        }
                    }
                }

                if (nearestDrain != null)
                    results[vertex] = (nearestDrain, minDistance);
            }

            return results;
        }

        private List<XYZ> DijkstraPath(
            SlabShapeVertex start,
            SlabShapeVertex end,
            Dictionary<SlabShapeVertex, List<SlabShapeVertex>> graph)
        {
            try
            {
                if (start == null || end == null || graph == null) return null;

                var dist = new Dictionary<SlabShapeVertex, double>();
                var prev = new Dictionary<SlabShapeVertex, SlabShapeVertex>();
                var queue = new List<(double distance, SlabShapeVertex vertex)> { (0.0, start) };

                foreach (var v in graph.Keys)
                {
                    if (v == null) continue;
                    dist[v] = double.PositiveInfinity;
                    prev[v] = null;
                }
                dist[start] = 0.0;

                while (queue.Count > 0)
                {
                    queue.Sort((x, y) => x.distance.CompareTo(y.distance));
                    var item = queue[0];
                    queue.RemoveAt(0);

                    var current = item.vertex;
                    if (current == end) break;

                    List<SlabShapeVertex> neighbors;
                    if (!graph.TryGetValue(current, out neighbors)) continue;

                    foreach (var neighbor in neighbors)
                    {
                        if (neighbor == null || current.Position == null || neighbor.Position == null) continue;

                        double alt = dist[current] + current.Position.DistanceTo(neighbor.Position);
                        double curDist;
                        if (!dist.TryGetValue(neighbor, out curDist)) curDist = double.PositiveInfinity;

                        if (alt < curDist)
                        {
                            dist[neighbor] = alt;
                            prev[neighbor] = current;

                            // update priority
                            for (int i = 0; i < queue.Count; i++)
                            {
                                if (queue[i].vertex == neighbor)
                                {
                                    queue.RemoveAt(i);
                                    break;
                                }
                            }
                            queue.Add((alt, neighbor));
                        }
                    }
                }

                if (!prev.ContainsKey(end) || (prev[end] == null && end != start)) return null;

                var path = new List<XYZ>();
                for (var v = end; v != null; v = prev.ContainsKey(v) ? prev[v] : null)
                {
                    if (v.Position != null) path.Insert(0, v.Position);
                    if (v == start) break;
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
                var taskDialog = new TaskDialog("Slope Percentage")
                {
                    MainInstruction = "Select roof slope percentage:",
                    CommonButtons = TaskDialogCommonButtons.Cancel
                };
                taskDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "2.0% (Default)");
                taskDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "1.5% (Minimum)");
                taskDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "1.0% (Minimum)");
                taskDialog.AddCommandLink(TaskDialogCommandLinkId.CommandLink4, "Custom value...");

                TaskDialogResult result = taskDialog.Show();

                switch (result)
                {
                    case TaskDialogResult.CommandLink1: return 2.0;
                    case TaskDialogResult.CommandLink2: return 1.5;
                    case TaskDialogResult.CommandLink3: return 1.0;
                    case TaskDialogResult.CommandLink4: return GetCustomSlopeValue();
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

                double value;
                if (double.TryParse(input, out value) && value > 0) return value;
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
                    var solid = geomObj as Solid;
                    if (solid == null) continue;

                    foreach (Face face in solid.Faces)
                    {
                        if (face == null) continue;

                        BoundingBoxUV bb = face.GetBoundingBox();
                        var uv = new UV((bb.Min.U + bb.Max.U) / 2.0, (bb.Min.V + bb.Max.V) / 2.0);
                        XYZ midpoint = face.Evaluate(uv);
                        if (midpoint != null && midpoint.Z > maxZ)
                        {
                            maxZ = midpoint.Z;
                            topFace = face;
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

                return bb != null &&
                       bb.Min != null &&
                       bb.Max != null &&
                       bb.Min.U <= uv.U && uv.U <= bb.Max.U &&
                       bb.Min.V <= uv.V && uv.V <= bb.Max.V;
            }
            catch
            {
                return false;
            }
        }

        private List<SlabShapeVertex> PickDrainPoints(UIDocument uidoc, List<SlabShapeVertex> vertices)
        {
            var drainPoints = new List<SlabShapeVertex>();
            try
            {
                if (uidoc == null || vertices == null) return drainPoints;

                TaskDialog.Show("Info", "Select Drain Points. Press ESC to finish selection.");
                IList<Reference> selectedRefs = uidoc.Selection.PickObjects(ObjectType.PointOnElement, "Select Drain Points");

                foreach (Reference vertexRef in selectedRefs)
                {
                    if (vertexRef == null) continue;

                    XYZ drainPoint = vertexRef.GlobalPoint;
                    var closestVertex = vertices
                        .Where(v => v != null && v.Position != null)
                        .OrderBy(v => v.Position.DistanceTo(drainPoint))
                        .FirstOrDefault();

                    if (closestVertex != null && !drainPoints.Contains(closestVertex))
                        drainPoints.Add(closestVertex);
                }
                return drainPoints;
            }
            catch
            {
                return drainPoints;
            }
        }

        // --------- ADAPTIVE GRAPH BUILD with simplified init ---------
        private Dictionary<SlabShapeVertex, List<SlabShapeVertex>> BuildGraph(List<SlabShapeVertex> vertices, Face topFace)
        {
            var graph = new Dictionary<SlabShapeVertex, List<SlabShapeVertex>>();
            try
            {
                if (vertices == null || topFace == null) return graph;

                double threshold = ComputeAdaptiveThreshold(vertices);

                foreach (var v in vertices)
                {
                    if (v == null || v.Position == null) continue;

                    // Build adjacency in one shot
                    var neighbors = vertices
                        .Where(o =>
                            o != null &&
                            o.Position != null &&
                            !ReferenceEquals(o, v) &&
                            v.Position.DistanceTo(o.Position) <= threshold &&
                            IsValidConnection(v.Position, o.Position, topFace))
                        .ToList();

                    graph[v] = neighbors;
                }
                return graph;
            }
            catch
            {
                return graph;
            }
        }

        private double ComputeAdaptiveThreshold(List<SlabShapeVertex> vertices)
        {
            var pts = vertices == null
                ? new List<SlabShapeVertex>()
                : vertices.Where(v => v != null && v.Position != null).ToList();

            if (pts.Count < 2) return 1.0; // feet (fallback)

            var nearestDistances = new List<double>(pts.Count);
            foreach (var v in pts)
            {
                double nn = pts
                    .Where(o => !ReferenceEquals(o, v))
                    .Select(o => v.Position.DistanceTo(o.Position))
                    .DefaultIfEmpty(double.PositiveInfinity)
                    .Min();

                if (!double.IsInfinity(nn) && nn > 0.0) nearestDistances.Add(nn);
            }

            if (nearestDistances.Count == 0) return 1.0;

            nearestDistances.Sort();
            double medianNN = (nearestDistances.Count % 2 == 1)
                ? nearestDistances[nearestDistances.Count / 2]
                : 0.5 * (nearestDistances[nearestDistances.Count / 2 - 1] + nearestDistances[nearestDistances.Count / 2]);

            double raw = medianNN * 2.5;
            double minClamp = medianNN * 1.25;
            double maxClamp = medianNN * 6.0;
            double absoluteMin = 0.5; // ~152mm

            double threshold = Math.Max(absoluteMin, Math.Min(Math.Max(raw, minClamp), maxClamp));
            return threshold;
        }

        private bool IsValidConnection(XYZ start, XYZ end, Face face)
        {
            try
            {
                if (start == null || end == null || face == null) return false;

                Line line = Line.CreateBound(start, end);
                if (line == null) return false;

                for (double t = 0.1; t < 1.0; t += 0.1)
                {
                    XYZ p = line.Evaluate(t, true);
                    if (!IsPointOnFace(p, face)) return false;
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
            public bool AllowElement(Element e) { return e is RoofBase; }
            public bool AllowReference(Reference r, XYZ p) { return false; }
        }
    }
}
