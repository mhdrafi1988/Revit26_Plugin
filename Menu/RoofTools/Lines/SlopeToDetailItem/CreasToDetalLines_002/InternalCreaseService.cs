using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    /// <summary>
    /// Extracts internal (non-boundary) edges.
    /// </summary>
    public class InternalCreaseService
    {
        public IList<FlattenedEdge2D> ExtractCreases(
            IList<FlattenedEdge2D> allEdges,
            ClassifiedRoofLoops classified,
            LoggingService log)
        {
            var boundaryEdges =
                classified.OuterLoop.Edges
                    .Concat(
                        classified.InnerLoops
                            .SelectMany(l => l.Edges))
                    .ToHashSet();

            var creases =
                allEdges
                    .Where(e => !boundaryEdges.Contains(e))
                    .ToList();

            log.Info($"Internal creases detected: {creases.Count}");
            return creases;
        }
    }
}
