using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using Revit26_Plugin.AutoSlopeByPoint_WIP2.Export; // <-- Update this to the actual namespace where AutoSlopeExportContext is defined
using Revit26_Plugin.AutoSlopeByPoint_WIP2.Services;

namespace Revit26_Plugin.AutoSlopeByPoint_WIP2.Handlers
{
    public class AutoSlopeHandler
    {
        private readonly Document _doc;

        public AutoSlopeHandler(Document doc)
        {
            _doc = doc;
        }

        public void Run(
            RoofBase roof,
            Element drain,
            IEnumerable<VertexPathResult> vertexPathResults,
            double slopePercent,
            string exportFolder)
        {
            // =====================================================
            // NEW: export context (safe, isolated)
            // =====================================================
            var exportContext =
                new AutoSlopeExportContext(exportFolder);

            int pointIndex = 0;

            // =====================================================
            // EXISTING LOOP (DO NOT TOUCH LOGIC)
            // =====================================================
            foreach (var vertexResult in vertexPathResults)
            {
                double pathLengthMeters = vertexResult.PathLengthMeters;

                // ?? REQUIRED: captured during slope calc
                double elevationOffset =
                    pathLengthMeters * (slopePercent / 100.0);

                // -------------------------------------------------
                // YOUR EXISTING SLOPE APPLICATION STAYS HERE
                // Example:
                // ApplySlope(roof, vertexResult, elevationOffset);
                // -------------------------------------------------

                // =================================================
                // NEW: export row (NO geometry)
                // =================================================
                exportContext.Rows.Add(new AutoSlopeVertexExportDto
                {
                    RoofElementId = (int)roof.Id.Value,
                    DrainElementId = (int)drain.Id.Value,
                    PointIndex = pointIndex,
                    PathLength = pathLengthMeters,
                    SlopePercent = slopePercent,
                    ElevationOffset = elevationOffset,
                    Direction = "Down"
                });

                pointIndex++;
            }

            // =====================================================
            // NEW: export AFTER slope completes
            // =====================================================
            exportContext.Commit();
        }
    }

    // ?? Map this to your REAL Dijkstra result
    public class VertexPathResult
    {
        public double PathLengthMeters { get; set; }
    }
}
