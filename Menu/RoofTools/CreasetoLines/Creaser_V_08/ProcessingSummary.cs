using System.Text;

namespace Revit26_Plugin.Creaser_V08.Commands.Models
{
    public class ProcessingSummary
    {
        public CornerSummary Corner { get; }
        public DrainSummary Drain { get; }
        public CreaseSummary Crease { get; }
        public LineSummary Line { get; }

        public ProcessingSummary(
            CornerSummary corner,
            DrainSummary drain,
            CreaseSummary crease,
            LineSummary line)
        {
            Corner = corner;
            Drain = drain;
            Crease = crease;
            Line = line;
        }

        public string BuildLog()
        {
            StringBuilder sb = new();

            sb.AppendLine($"Total boundary corners: {Corner.TotalCorners}");
            sb.AppendLine($"Shape points scanned: {Drain.ShapePointsScanned}");
            sb.AppendLine($"Lowest Z: {Drain.LowestZ}");
            sb.AppendLine($"Raw drain candidates: {Drain.RawCandidates}");
            sb.AppendLine($"Drain clusters count: {Drain.ClusterCount}");
            sb.AppendLine($"Final drain count: {Drain.FinalDrains}");
            sb.AppendLine($"Corners processed: {Corner.TotalCorners}");
            sb.AppendLine($"Valid crease paths: {Crease.ValidPaths}");
            sb.AppendLine($"Failed crease paths: {Crease.FailedPaths}");
            sb.AppendLine($"Lines created: {Line.LinesCreated}");
            sb.AppendLine($"Duplicate lines removed: {Line.DuplicatesRemoved}");
            sb.AppendLine($"Final lines placed: {Line.FinalPlaced}");

            return sb.ToString();
        }
    }
}
