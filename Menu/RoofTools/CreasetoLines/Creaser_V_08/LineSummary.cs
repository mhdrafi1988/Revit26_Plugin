namespace Revit26_Plugin.Creaser_V08.Commands.Models
{
    public class LineSummary
    {
        public int LinesCreated { get; }
        public int DuplicatesRemoved { get; }
        public int FinalPlaced { get; }

        public LineSummary(int linesCreated, int duplicatesRemoved, int finalPlaced)
        {
            LinesCreated = linesCreated;
            DuplicatesRemoved = duplicatesRemoved;
            FinalPlaced = finalPlaced;
        }
    }
}
