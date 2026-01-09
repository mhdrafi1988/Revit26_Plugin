using CommunityToolkit.Mvvm.ComponentModel;

namespace Revit26_Plugin.RoofTools.LineAndPoints.RoofRidgeLines_V12.ViewModels
{
    /// <summary>
    /// ViewModel responsible ONLY for displaying execution results.
    /// No Revit API access. Pure WPF/MVVM.
    /// </summary>
    public partial class SummaryViewModel : ObservableObject
    {
        [ObservableProperty]
        private bool isVisible;

        [ObservableProperty]
        private bool success;

        [ObservableProperty]
        private int shapePointsAdded;

        [ObservableProperty]
        private string summaryMessage;

        public void Clear()
        {
            IsVisible = false;
            Success = false;
            ShapePointsAdded = 0;
            SummaryMessage = string.Empty;
        }

        public void SetSuccess(int pointsAdded)
        {
            Success = true;
            ShapePointsAdded = pointsAdded;
            SummaryMessage = $"Completed successfully.\nPoints added: {pointsAdded}";
            IsVisible = true;
        }

        public void SetFailure(string message)
        {
            Success = false;
            ShapePointsAdded = 0;
            SummaryMessage = message;
            IsVisible = true;
        }
    }
}
