using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Revit26_Plugin.TaperedEpsPattern2D.V001.Core.Pattern;

namespace Revit26_Plugin.TaperedEpsPattern2D.V001.ViewModels
{
    /// <summary>Board Settings card: board size, slope, minimum thickness, grid direction and origin.</summary>
    public sealed partial class BoardSettingsViewModel : ObservableObject
    {
        public BoardSettingsViewModel()
        {
            Width = new NumericFieldViewModel("Board width", "m", Pattern2DSettings.MinBoardSizeM, Pattern2DSettings.MaxBoardSizeM, 1.0, "0.00");
            Length = new NumericFieldViewModel("Board length", "m", Pattern2DSettings.MinBoardSizeM, Pattern2DSettings.MaxBoardSizeM, 1.0, "0.00");
            Slope = new NumericFieldViewModel("Slope", "%", SettingsValidator.MinSlopePercent, SettingsValidator.MaxSlopePercent, 1.0, "0.0#");
            MinThickness = new NumericFieldViewModel("Min thickness", "mm", 0, SettingsValidator.MaxMinThicknessMm, 20.0, "0");

            foreach (NumericFieldViewModel f in Fields)
            {
                f.ValueChanged += (_, _) => RaiseChanged();
            }

            _selectedGridDirection = GridDirections[0];
            _selectedGridOrigin = GridOrigins[0];
        }

        public NumericFieldViewModel Width { get; }
        public NumericFieldViewModel Length { get; }
        public NumericFieldViewModel Slope { get; }
        public NumericFieldViewModel MinThickness { get; }

        public IReadOnlyList<NumericFieldViewModel> Fields => new[] { Width, Length, Slope, MinThickness };

        public IReadOnlyList<ChoiceOption<GridDirectionMode>> GridDirections { get; } = new[]
        {
            new ChoiceOption<GridDirectionMode>(GridDirectionMode.LongestEdge, "Along longest edge"),
            new ChoiceOption<GridDirectionMode>(GridDirectionMode.ProjectX, "Project X")
        };

        public IReadOnlyList<ChoiceOption<GridOriginMode>> GridOrigins { get; } = new[]
        {
            new ChoiceOption<GridOriginMode>(GridOriginMode.BoundingBoxCorner, "Bounding-box corner"),
            new ChoiceOption<GridOriginMode>(GridOriginMode.Centred, "Centred")
        };

        [ObservableProperty]
        private ChoiceOption<GridDirectionMode> _selectedGridDirection;

        [ObservableProperty]
        private ChoiceOption<GridOriginMode> _selectedGridOrigin;

        /// <summary>Locked: this version always drains to the perimeter.</summary>
        public string WaterFallsTo => "Perimeter (all edges)";

        public bool HasErrors => Fields.Any(f => f.HasError);

        public event EventHandler? Changed;

        public Pattern2DSettings ApplyTo(Pattern2DSettings settings) => settings with
        {
            BoardWidthM = Width.Value ?? Width.Default,
            BoardLengthM = Length.Value ?? Length.Default,
            SlopePercent = Slope.Value ?? Slope.Default,
            MinThicknessMm = MinThickness.Value ?? MinThickness.Default,
            GridDirection = SelectedGridDirection.Value,
            GridOrigin = SelectedGridOrigin.Value
        };

        public void Reset()
        {
            foreach (NumericFieldViewModel f in Fields)
            {
                f.Reset();
            }

            SelectedGridDirection = GridDirections[0];
            SelectedGridOrigin = GridOrigins[0];
        }

        partial void OnSelectedGridDirectionChanged(ChoiceOption<GridDirectionMode> value) => RaiseChanged();

        partial void OnSelectedGridOriginChanged(ChoiceOption<GridOriginMode> value) => RaiseChanged();

        private void RaiseChanged()
        {
            OnPropertyChanged(nameof(HasErrors));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
