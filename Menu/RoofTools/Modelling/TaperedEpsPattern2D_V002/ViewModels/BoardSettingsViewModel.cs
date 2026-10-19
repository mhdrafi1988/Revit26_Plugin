using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Revit26_Plugin.TaperedEpsPattern2D.V002.Core.Pattern;

namespace Revit26_Plugin.TaperedEpsPattern2D.V002.ViewModels
{
    /// <summary>
    /// Board Settings card: board size, slope, minimum thickness, grid alignment and origin.
    /// V002: the grid can run parallel to any roof edge picked from a list (default = longest edge),
    /// or each slope face gets its own grid parallel to its own edge ("Each edge").
    /// </summary>
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
            new ChoiceOption<GridDirectionMode>(GridDirectionMode.SelectedEdge, "Parallel to one roof edge"),
            new ChoiceOption<GridDirectionMode>(GridDirectionMode.EachEdge, "Each edge (own grid per slope)"),
            new ChoiceOption<GridDirectionMode>(GridDirectionMode.ProjectX, "Project X")
        };

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsEdgePickerEnabled))]
        [NotifyPropertyChangedFor(nameof(HighlightEdgeIndex))]
        private ChoiceOption<GridDirectionMode> _selectedGridDirection;

        /// <summary>Edges of the current roof outline (CCW, collinear sketch lines merged).</summary>
        public ObservableCollection<OutlineEdgeInfo> Edges { get; } = new ObservableCollection<OutlineEdgeInfo>();

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(HighlightEdgeIndex))]
        private OutlineEdgeInfo? _selectedEdge;

        public bool IsEdgePickerEnabled => SelectedGridDirection.Value == GridDirectionMode.SelectedEdge && Edges.Count > 0;

        /// <summary>Edge to highlight in the preview (−1 = none).</summary>
        public int HighlightEdgeIndex =>
            SelectedGridDirection.Value == GridDirectionMode.SelectedEdge && SelectedEdge != null ? SelectedEdge.Index : -1;

        public IReadOnlyList<ChoiceOption<GridOriginMode>> GridOrigins { get; } = new[]
        {
            new ChoiceOption<GridOriginMode>(GridOriginMode.BoundingBoxCorner, "Corner (flush with edges)"),
            new ChoiceOption<GridOriginMode>(GridOriginMode.Centred, "Centred")
        };

        [ObservableProperty]
        private ChoiceOption<GridOriginMode> _selectedGridOrigin;

        /// <summary>Locked: this version always drains to the perimeter.</summary>
        public string WaterFallsTo => "Perimeter (all edges)";

        public bool HasErrors => Fields.Any(f => f.HasError);

        public event EventHandler? Changed;

        /// <summary>
        /// Loads the edges of a (new or re-read) outline. Keeps the chosen edge when it still exists,
        /// otherwise selects the longest edge.
        /// </summary>
        public void SetOutline(Polygon2D? outline)
        {
            int previous = SelectedEdge?.Index ?? -1;
            Edges.Clear();
            if (outline != null && !outline.IsEmpty)
            {
                foreach (OutlineEdgeInfo e in OutlineEdgeInfo.Describe(outline))
                {
                    Edges.Add(e);
                }
            }

            SelectedEdge = Edges.FirstOrDefault(e => e.Index == previous) ?? Edges.FirstOrDefault(e => e.IsLongest);
            OnPropertyChanged(nameof(IsEdgePickerEnabled));
        }

        public Pattern2DSettings ApplyTo(Pattern2DSettings settings)
        {
            GridDirectionMode direction = SelectedGridDirection.Value;
            if (direction == GridDirectionMode.SelectedEdge && SelectedEdge == null)
            {
                direction = GridDirectionMode.LongestEdge;
            }

            return settings with
            {
                BoardWidthM = Width.Value ?? Width.Default,
                BoardLengthM = Length.Value ?? Length.Default,
                SlopePercent = Slope.Value ?? Slope.Default,
                MinThicknessMm = MinThickness.Value ?? MinThickness.Default,
                GridDirection = direction,
                AlignEdgeIndex = SelectedEdge?.Index ?? 0,
                GridOrigin = SelectedGridOrigin.Value
            };
        }

        public void Reset()
        {
            foreach (NumericFieldViewModel f in Fields)
            {
                f.Reset();
            }

            SelectedGridDirection = GridDirections[0];
            SelectedEdge = Edges.FirstOrDefault(e => e.IsLongest);
            SelectedGridOrigin = GridOrigins[0];
        }

        partial void OnSelectedGridDirectionChanged(ChoiceOption<GridDirectionMode> value)
        {
            OnPropertyChanged(nameof(IsEdgePickerEnabled));
            RaiseChanged();
        }

        partial void OnSelectedEdgeChanged(OutlineEdgeInfo? value) => RaiseChanged();

        partial void OnSelectedGridOriginChanged(ChoiceOption<GridOriginMode> value) => RaiseChanged();

        private void RaiseChanged()
        {
            OnPropertyChanged(nameof(HasErrors));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
