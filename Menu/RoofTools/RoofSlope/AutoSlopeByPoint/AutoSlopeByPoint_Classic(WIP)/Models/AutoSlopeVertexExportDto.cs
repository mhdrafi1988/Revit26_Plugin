namespace Revit26_Plugin.AutoSlopeByPoint_WIP2.Export
{
    public class AutoSlopeVertexExportDto
    {
        public int RoofElementId { get; set; }
        public int DrainElementId { get; set; }
        public int PointIndex { get; set; }
        public double PathLength { get; set; }
        public double SlopePercent { get; set; }
        public double ElevationOffset { get; set; }
        public string Direction { get; set; }
    }
}