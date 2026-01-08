// File: SummaryStepViewModel.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.ViewModels.Steps
//
// Responsibility:
// - Displays execution summary
// - Read-only presentation model
//
// IMPORTANT:
// - NO Revit API
// - NO business logic

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

using Revit26_Plugin.RoofRidgeLines_V06.Models;

namespace Revit26_Plugin.RoofRidgeLines_V06.ViewModels.Steps
{
    public class SummaryStepViewModel : ObservableObject
    {
        private int _detailLinesCreated;
        private int _shapePointsAdded;
        private double _executionTimeSeconds;

        public int DetailLinesCreated
        {
            get => _detailLinesCreated;
            set => SetProperty(ref _detailLinesCreated, value);
        }

        public int ShapePointsAdded
        {
            get => _shapePointsAdded;
            set => SetProperty(ref _shapePointsAdded, value);
        }

        public double ExecutionTimeSeconds
        {
            get => _executionTimeSeconds;
            set => SetProperty(ref _executionTimeSeconds, value);
        }

        public ObservableCollection<string> Warnings { get; } = new();

        public void LoadFromResult(ExecutionResult result)
        {
            DetailLinesCreated = result.DetailLinesCreated;
            ShapePointsAdded = result.ShapePointsAdded;
            ExecutionTimeSeconds = result.ExecutionTimeSeconds;

            Warnings.Clear();
            foreach (string warning in result.Warnings)
            {
                Warnings.Add(warning);
            }
        }
    }
}
