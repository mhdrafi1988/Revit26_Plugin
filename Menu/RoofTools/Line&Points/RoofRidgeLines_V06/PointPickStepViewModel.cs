// File: PointPickStepViewModel.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.ViewModels.Steps
//
// Responsibility:
// - Holds picked point data for Step 2
// - Computes live distance preview
// - Enforces minimum distance requirement
//
// IMPORTANT:
// - NO Revit API references
// - Pure MVVM logic

using System;
using CommunityToolkit.Mvvm.ComponentModel;

using Revit26_Plugin.RoofRidgeLines_V06.Models;

namespace Revit26_Plugin.RoofRidgeLines_V06.ViewModels.Steps
{
    public class PointPickStepViewModel : ObservableObject
    {
        private const double MinimumDistanceMeters = 1.0;

        private PickedPointData _point1;
        private PickedPointData _point2;
        private double _distanceMeters;
        private bool _isValid;

        /// <summary>
        /// First picked point.
        /// </summary>
        public PickedPointData Point1
        {
            get => _point1;
            set
            {
                SetProperty(ref _point1, value);
                Recalculate();
            }
        }

        /// <summary>
        /// Second picked point.
        /// </summary>
        public PickedPointData Point2
        {
            get => _point2;
            set
            {
                SetProperty(ref _point2, value);
                Recalculate();
            }
        }

        /// <summary>
        /// Distance between picked points in meters (preview).
        /// </summary>
        public double DistanceMeters
        {
            get => _distanceMeters;
            private set => SetProperty(ref _distanceMeters, value);
        }

        /// <summary>
        /// Indicates whether point selection is valid.
        /// </summary>
        public bool IsValid
        {
            get => _isValid;
            private set => SetProperty(ref _isValid, value);
        }

        private void Recalculate()
        {
            if (Point1 == null || Point2 == null)
            {
                DistanceMeters = 0;
                IsValid = false;
                return;
            }

            double dx = Point2.X - Point1.X;
            double dy = Point2.Y - Point1.Y;
            double dz = Point2.Z - Point1.Z;

            double distanceFeet = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            // Revit internal units → meters
            DistanceMeters = distanceFeet * 0.3048;

            IsValid = DistanceMeters >= MinimumDistanceMeters;
        }
    }
}
