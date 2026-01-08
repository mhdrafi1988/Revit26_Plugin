using Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.Models;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V11.ViewModels
{
    public class SummaryViewModel
    {
        public SummaryViewModel(RoofData d)
        {
            Duration = d.Duration.ToString(@"mm\:ss");
            Points = d.ShapePointsAdded;
            Success = d.IsSuccess;
        }

        public string Duration { get; }
        public int Points { get; }
        public bool Success { get; }
    }
}
