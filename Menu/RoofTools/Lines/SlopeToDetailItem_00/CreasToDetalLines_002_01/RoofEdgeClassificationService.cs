using System;
using System.Collections.Generic;
using System.Linq;

namespace Revit26_Plugin.CreaserAdv_V002.Services
{
    /// <summary>
    /// Classifies closed loops into outer boundary and inner voids.
    /// </summary>
    public class RoofEdgeClassificationService
    {
        public ClassifiedRoofLoops Classify(
            IList<EdgeLoop2D> loops,
            LoggingService log)
        {
            if (loops == null || loops.Count == 0)
                throw new ArgumentException("No loops to classify.");

            log.Info($"Classifying {loops.Count} closed loops.");

            var loopAreas = new Dictionary<EdgeLoop2D, double>();

            foreach (EdgeLoop2D loop in loops)
            {
                double area =
                    LoopAreaCalculator.ComputeArea(loop.Edges);

                loopAreas.Add(loop, Math.Abs(area));

                log.Info(
                    $"Loop area computed: {Math.Abs(area):F3}");
            }

            // -----------------------------
            // Outer loop = largest area
            // -----------------------------
            EdgeLoop2D outer =
                loopAreas
                    .OrderByDescending(kvp => kvp.Value)
                    .First()
                    .Key;

            var result = new ClassifiedRoofLoops
            {
                OuterLoop = outer
            };

            foreach (EdgeLoop2D loop in loops)
            {
                if (!ReferenceEquals(loop, outer))
                    result.InnerLoops.Add(loop);
            }

            log.Info(
                $"Outer loop selected. Inner voids: {result.InnerLoops.Count}");

            return result;
        }
    }
}
