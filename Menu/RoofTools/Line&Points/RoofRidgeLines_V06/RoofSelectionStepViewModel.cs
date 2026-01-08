// File: RoofSelectionStepViewModel.cs
// Namespace: Revit26_Plugin.RoofRidgeLines_V06.ViewModels.Steps
//
// Responsibility:
// - Holds roof selection state for Step 1
// - Exposes validation flags to the UI
// - Receives RoofInfo from selection service (indirectly)
//
// IMPORTANT:
// - NO Revit API references
// - NO service calls

using CommunityToolkit.Mvvm.ComponentModel;

using Revit26_Plugin.RoofRidgeLines_V06.Models;

namespace Revit26_Plugin.RoofRidgeLines_V06.ViewModels.Steps
{
    public class RoofSelectionStepViewModel : ObservableObject
    {
        private RoofInfo _selectedRoof;
        private bool _isValid;

        /// <summary>
        /// Selected roof information (API-agnostic).
        /// </summary>
        public RoofInfo SelectedRoof
        {
            get => _selectedRoof;
            set
            {
                SetProperty(ref _selectedRoof, value);
                Validate();
            }
        }

        /// <summary>
        /// Indicates whether the step is valid and user can proceed.
        /// </summary>
        public bool IsValid
        {
            get => _isValid;
            private set => SetProperty(ref _isValid, value);
        }

        private void Validate()
        {
            IsValid =
                SelectedRoof != null &&
                SelectedRoof.IsShapeEditable;
        }
    }
}
