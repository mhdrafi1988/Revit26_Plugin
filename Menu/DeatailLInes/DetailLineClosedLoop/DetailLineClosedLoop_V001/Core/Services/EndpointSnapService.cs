using System.Collections.Generic;
using System.Collections.ObjectModel;
using Autodesk.Revit.DB;
using Revit26_Plugin.DetailLineClosedLoop.V001.Core.Geometry;
using Revit26_Plugin.Shared.Models;

namespace Revit26_Plugin.DetailLineClosedLoop.V001.Core.Services
{
    /// <summary>
    /// Step 5 — when enabled, snaps endpoints that fall within Revit's short
    /// curve tolerance of each other onto a single shared point, closing tiny
    /// numeric gaps left by the trim/extend/merge passes.
    /// </summary>
    public static class EndpointSnapService
    {
        public static List<Curve> Snap(List<Curve> curves, double tolerance, out int snappedCount, ObservableCollection<LogEntry> log)
        {
            EndpointGraph graph = EndpointGraph.Build(curves, tolerance);
            var result = new List<Curve>(curves.Count);
            int snapped = 0;

            for (int i = 0; i < curves.Count; i++)
            {
                Curve c = curves[i];
                (int startNode, int endNode) = graph.Edges[i];
                XYZ newStart = graph.Nodes[startNode];
                XYZ newEnd = graph.Nodes[endNode];

                bool startMoved = c.GetEndPoint(0).DistanceTo(newStart) > 1e-9;
                bool endMoved = c.GetEndPoint(1).DistanceTo(newEnd) > 1e-9;

                if (!startMoved && !endMoved)
                {
                    result.Add(c);
                    continue;
                }

                if (newStart.DistanceTo(newEnd) <= tolerance)
                    continue; // snapping collapsed this curve to a point — drop it

                Curve rebuilt = RebuildCurve(c, newStart, newEnd);
                if (rebuilt == null)
                {
                    result.Add(c);
                    continue;
                }

                result.Add(rebuilt);
                if (startMoved) snapped++;
                if (endMoved) snapped++;
            }

            snappedCount = snapped;
            if (snapped > 0)
                log.Add(new LogEntry(LogLevel.Info, $"Endpoint snap: {snapped} vertex movement(s) applied (< {tolerance:F4} ft)"));

            return result;
        }

        private static Curve RebuildCurve(Curve original, XYZ newStart, XYZ newEnd)
        {
            if (original is Line)
                return Line.CreateBound(newStart, newEnd);

            if (original is Arc arc)
                return Arc.Create(newStart, newEnd, arc.Evaluate(0.5, true));

            return null;
        }
    }
}
