namespace Revit26_Plugin.Creaser_V08.Commands.Models
{
    public class CreaseSummary
    {
        public int ValidPaths { get; }
        public int FailedPaths { get; }

        public CreaseSummary(int validPaths, int failedPaths)
        {
            ValidPaths = validPaths;
            FailedPaths = failedPaths;
        }
    }
}
