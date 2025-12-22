// ============================================================
// File: CreaserCommand.cs
// Namespace: Revit26_Plugin.Creaser_V07.Commands
// ============================================================

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Revit26_Plugin.Creaser_V07.Commands
{
    [Transaction(TransactionMode.Manual)]
    public class CreaserCommand : IExternalCommand
    {
        public Result Execute(
            ExternalCommandData commandData,
            ref string message,
            ElementSet elements)
        {
            UIDocument uiDoc = commandData.Application.ActiveUIDocument;
            Document doc = uiDoc?.Document;
            View view = doc?.ActiveView;

            if (uiDoc == null || doc == null || view == null)
            {
                message = "Revit context invalid (UIDocument/Document/View).";
                return Result.Failed;
            }

            // Requirement: place detail lines in the active plan view.
            if (view.ViewType != ViewType.FloorPlan &&
                view.ViewType != ViewType.CeilingPlan &&
                view.ViewType != ViewType.EngineeringPlan &&
                view.ViewType != ViewType.AreaPlan)
            {
                message = "Active view must be a plan view (Floor/Ceiling/Engineering/Area).";
                return Result.Failed;
            }

            // ------------------------------
            // Select roof
            // ------------------------------
            Reference picked;
            try
            {
                picked = uiDoc.Selection.PickObject(
                    ObjectType.Element,
                    new RoofSelectionFilter(),
                    "Select a roof");
            }
            catch
            {
                return Result.Cancelled;
            }

            RoofBase roof = doc.GetElement(picked) as RoofBase;
            if (roof == null)
            {
                message = "Selected element is not a roof.";
                return Result.Failed;
            }

            var log = new StringBuilder();
            var stats = new WorkflowStats();

            // ------------------------------
            // Build graph + Point Set-01 (corners) + Point Set-02 (drains)
            // ------------------------------
            RoofGraphBuilder builder = new RoofGraphBuilder();
            RoofGraphData data = builder.Build(roof);

            // Step 1: boundary corners => Point Set-01
            IReadOnlyCollection<XYZKey> pointSet01 = data.BoundaryCorners;
            stats.TotalCornersDetected = pointSet01.Count;

            // Step 2: drains => Point Set-02
            IReadOnlyCollection<XYZKey> pointSet02 = data.Drains;
            stats.TotalDrainCandidates = data.DrainCandidatesCount; // before unique
            stats.UniqueDrains = pointSet02.Count;
            stats.IgnoredDrainPoints = data.AllNodesCount - pointSet02.Count;

            // Guard
            if (data.Graph.Count == 0 || data.AllNodesCount == 0)
            {
                message = "No usable roof geometry graph was created (no downward faces/edges found).";
                return Result.Failed;
            }

            if (pointSet01.Count == 0)
            {
                message = "No roof boundary corner points detected (Point Set-01 empty).";
                return Result.Failed;
            }

            if (pointSet02.Count == 0)
            {
                message = "No lowest elevation drain points detected (Point Set-02 empty).";
                return Result.Failed;
            }

            // ------------------------------
            // Step 3: For each corner -> nearest drain -> Dijkstra path
            // Store paths as Set-03
            // ------------------------------
            List<List<XYZKey>> set03Paths = new();
            double totalPathLen = 0.0;

            foreach (XYZKey corner in pointSet01)
            {
                // If the corner is itself a drain, path length = 0
                if (pointSet02.Contains(corner))
                {
                    stats.CornersSkippedAlreadyDrain++;
                    continue;
                }

                XYZKey nearestDrain = NearestPointFinder.FindNearest(corner, pointSet02);

                // If corner isn't in the graph (can happen if boundary extracted from endpoints but
                // graph nodes are only tessellated points), snap to nearest node in graph.
                XYZKey startNode = data.NodeIndex.TryGetValue(corner, out var exact)
                    ? exact
                    : NearestPointFinder.FindNearest(corner, data.AllNodes);

                if (startNode.Equals(nearestDrain))
                {
                    stats.CornersSkippedAlreadyDrain++;
                    continue;
                }

                List<XYZKey> path = DijkstraSolver.FindShortestPath(
                    startNode,
                    nearestDrain,
                    data.Graph);

                if (path.Count < 2)
                {
                    stats.PathsFailed++;
                    continue;
                }

                set03Paths.Add(path);
                stats.PathsFound++;

                double len = PathMetrics.ComputePolylineLength(path);
                totalPathLen += len;
            }

            stats.AveragePathLength = stats.PathsFound > 0
                ? totalPathLen / stats.PathsFound
                : 0.0;

            // ------------------------------
            // Step 4: Extract line segments from Set-03
            // ------------------------------
            var extractedSegments = new List<EdgeSegment>();

            foreach (var path in set03Paths)
            {
                for (int i = 0; i < path.Count - 1; i++)
                {
                    extractedSegments.Add(new EdgeSegment(path[i], path[i + 1]));
                }
            }

            stats.TotalSegmentsExtracted = extractedSegments.Count;

            if (extractedSegments.Count == 0)
            {
                message = BuildSummaryAndMessage(log, stats, "No segments extracted from paths (Set-03 empty).");
                TaskDialog.Show("Creaser Summary", message);
                return Result.Failed;
            }

            // ------------------------------
            // Step 5: Remove duplicate segments (direction-independent, tolerance-based)
            // ------------------------------
            var unique = new HashSet<EdgeKey>();
            var uniqueSegments = new List<EdgeSegment>();

            foreach (var s in extractedSegments)
            {
                var key = new EdgeKey(s.A, s.B);
                if (unique.Add(key))
                    uniqueSegments.Add(s);
                else
                    stats.DuplicatesRemoved++;
            }

            stats.RemainingLinesAfterDedup = uniqueSegments.Count;

            // ------------------------------
            // Step 6: Re-arrange direction high Z -> low Z (Set-04)
            // ------------------------------
            var set04Ordered = new List<EdgeSegment>();

            foreach (var seg in uniqueSegments)
            {
                if (Math.Abs(seg.A.Z - seg.B.Z) < 1e-9 && seg.A.Equals(seg.B))
                {
                    stats.LinesSkippedDegenerate++;
                    continue;
                }

                // Start = highest Z, End = lowest Z
                if (seg.A.Z >= seg.B.Z)
                {
                    set04Ordered.Add(seg);
                    stats.LinesReorderedOk++;
                }
                else
                {
                    set04Ordered.Add(new EdgeSegment(seg.B, seg.A));
                    stats.LinesReorderedOk++;
                }
            }

            if (set04Ordered.Count == 0)
            {
                message = BuildSummaryAndMessage(log, stats, "All segments were skipped as degenerate.");
                TaskDialog.Show("Creaser Summary", message);
                return Result.Failed;
            }

            // ------------------------------
            // Step 7: Place detail lines in active plan view (one per ordered segment)
            // ------------------------------
            int placed = 0;

            using (Transaction tx = new Transaction(doc, "Creaser - Place Detail Lines"))
            {
                tx.Start();

                foreach (var seg in set04Ordered)
                {
                    // Revit detail line needs 2D planar view, but geometry points are 3D.
                    // In plan, Revit will project them to the view's sketch plane.
                    Line line = Line.CreateBound(seg.A.ToXYZ(), seg.B.ToXYZ());
                    DetailCurve dc = doc.Create.NewDetailCurve(view, line);
                    if (dc != null) placed++;
                }

                tx.Commit();
            }

            stats.DetailLinesPlaced = placed;

            message = BuildSummaryAndMessage(log, stats, "Succeeded.");
            TaskDialog.Show("Creaser Summary", message);

            return Result.Succeeded;
        }

        private static string BuildSummaryAndMessage(StringBuilder log, WorkflowStats s, string status)
        {
            log.AppendLine($"STATUS: {status}");
            log.AppendLine();

            log.AppendLine("Step 1 – Point Set-01 (Boundary Corners)");
            log.AppendLine($"  Total corners detected: {s.TotalCornersDetected}");
            log.AppendLine($"  Skipped (corner is already drain): {s.CornersSkippedAlreadyDrain}");
            log.AppendLine($"  Valid corners processed: {Math.Max(0, s.TotalCornersDetected - s.CornersSkippedAlreadyDrain)}");
            log.AppendLine();

            log.AppendLine("Step 2 – Point Set-02 (Drain Points)");
            log.AppendLine($"  Total drains (candidates): {s.TotalDrainCandidates}");
            log.AppendLine($"  Unique drains: {s.UniqueDrains}");
            log.AppendLine($"  Ignored points (non-drains): {s.IgnoredDrainPoints}");
            log.AppendLine();

            log.AppendLine("Step 3 – Set-03 (Paths)");
            log.AppendLine($"  Paths found: {s.PathsFound}");
            log.AppendLine($"  Paths failed: {s.PathsFailed}");
            log.AppendLine($"  Average path length: {s.AveragePathLength:0.###} (model units)");
            log.AppendLine();

            log.AppendLine("Step 4 – Segments Extracted");
            log.AppendLine($"  Total segments extracted: {s.TotalSegmentsExtracted}");
            log.AppendLine();

            log.AppendLine("Step 5 – Deduplication");
            log.AppendLine($"  Duplicates removed: {s.DuplicatesRemoved}");
            log.AppendLine($"  Remaining lines: {s.RemainingLinesAfterDedup}");
            log.AppendLine();

            log.AppendLine("Step 6 – Direction Reorder (Set-04)");
            log.AppendLine($"  Lines reordered successfully: {s.LinesReorderedOk}");
            log.AppendLine($"  Skipped degenerate lines: {s.LinesSkippedDegenerate}");
            log.AppendLine();

            log.AppendLine("Step 7 – Placement");
            log.AppendLine($"  Detail lines placed: {s.DetailLinesPlaced}");

            return log.ToString();
        }
    }
}
