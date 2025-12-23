namespace Revit26_Plugin.Creaser_V08.Commands.Models
{
    public class DrainSummary
    {
        public int ShapePointsScanned { get; }
        public double LowestZ { get; }
        public int RawCandidates { get; }
        public int ClusterCount { get; }
        public int FinalDrains { get; }

        public DrainSummary(
            int shapePointsScanned,
            double lowestZ,
            int rawCandidates,
            int clusterCount,
            int finalDrains)
        {
            ShapePointsScanned = shapePointsScanned;
            LowestZ = lowestZ;
            RawCandidates = rawCandidates;
            ClusterCount = clusterCount;
            FinalDrains = finalDrains;
        }
    }
}
