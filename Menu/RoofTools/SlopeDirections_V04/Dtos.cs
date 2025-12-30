using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace Revit_26.CornertoDrainArrow_V05
{
    public sealed class RoofCornerPointDto
    {
        public ElementId RoofId { get; init; }
        public XYZ Point { get; init; }
        public double Elevation { get; init; }
        public int Index { get; init; }
        public string Source { get; init; }
    }

    public sealed class RoofDrainPointDto
    {
        public ElementId RoofId { get; init; }
        public XYZ Point { get; init; }
        public double Elevation { get; init; }
        public int Index { get; init; }
    }

    public sealed class WaterPathDto
    {
        public RoofCornerPointDto Start { get; }
        public RoofDrainPointDto End { get; }
        public IList<XYZ> PathPoints { get; } = new List<XYZ>();

        public bool IsValid { get; set; } = true;
        public string InvalidReason { get; set; }

        public WaterPathDto(RoofCornerPointDto start, RoofDrainPointDto end)
        {
            Start = start;
            End = end;
        }
    }

    public sealed class RoofDrainageRunResult
    {
        public int CornerCount { get; set; }
        public int DrainCount { get; set; }
        public int TotalPaths { get; set; }
        public int ValidPaths { get; set; }
        public int PlacedDetails { get; set; }
    }

    public class RoofDrainageRunOptions
    {
        public bool PlaceDetails { get; set; }
        public double MinSlope { get; set; } = 0.5;
    }

    public class DetailPlacementResultDto
    {
        public WaterPathDto Path { get; }
        public bool Success { get; set; }
        public int PlacedCount { get; set; }
        public string ErrorMessage { get; set; }

        public DetailPlacementResultDto(WaterPathDto path)
        {
            Path = path;
        }
    }

    public class VoidPolygonDto
    {
        public IList<XYZ> Points { get; set; } = new List<XYZ>();
    }
}