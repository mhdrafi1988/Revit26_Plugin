using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using Revit26_Plugin_Revised.TaperedEpsPattern2D.V004.Core.Pattern;

namespace Revit26_Plugin_Revised.TaperedEpsPattern2D.V004.ViewModels
{
    /// <summary>
    /// Board Settings card: board size, slope, minimum thickness, grid alignment and origin.
    /// V004: in contour-rows mode the user picks where rows start and where the leftover cut goes
    /// (A middle filler / B split at hips / C one hip / D from the middle, with D1 / D2 / Auto).
    /// V003 default: contour rows — each slope face is laid in rows between the thickness contours,
    /// full boards from both hips inward, mirrored hip pieces, one filler mid-row.
    /// Single-grid modes (parallel to a picked edge, or Project X) are kept.
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
            _selectedRowStart = RowStarts[0];
            _selectedMiddleVariant = MiddleVariants[0];
            MinPiece.ValueChanged += (_, _) => RaiseChanged();
        }

        public NumericFieldViewModel Width { get; }
        public NumericFieldViewModel Length { get; }
        public NumericFieldViewModel Slope { get; }
        public NumericFieldViewModel MinThickness { get; }

        public IReadOnlyList<NumericFieldViewModel> Fields => new[] { Width, Length, Slope, MinThickness };

        public IReadOnlyList<ChoiceOption<GridDirectionMode>> GridDirections { get; } = new[]
        {
            new ChoiceOption<GridDirectionMode>(GridDirectionMode.EachEdge, "Contour rows (each edge, mirrored hips)"),
            new ChoiceOption<GridDirectionMode>(GridDirectionMode.SelectedEdge, "One grid parallel to a roof edge"),
            new ChoiceOption<GridDirectionMode>(GridDirectionMode.ProjectX, "One grid along Project X")
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

        // ── V004: row start / leftover cut (contour-rows mode only) ──
        public IReadOnlyList<ChoiceOption<RowStartMode>> RowStarts { get; } = new[]
        {
            new ChoiceOption<RowStartMode>(RowStartMode.HipsWithMiddleFiller, "A · From both hips, filler in middle (mirrored hips)"),
            new ChoiceOption<RowStartMode>(RowStartMode.HipsSplitLeftover, "B · From both hips, leftover split at hips (no middle cut)"),
            new ChoiceOption<RowStartMode>(RowStartMode.OneHip, "C · From left hip, leftover at right-hand hip (no middle cut)"),
            new ChoiceOption<RowStartMode>(RowStartMode.FromMiddle, "D · From exact middle of row (no middle cut)")
        };

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(IsMiddleVariantEnabled))]
        private ChoiceOption<RowStartMode> _selectedRowStart;

        public IReadOnlyList<ChoiceOption<MiddleStartVariant>> MiddleVariants { get; } = new[]
        {
            new ChoiceOption<MiddleStartVariant>(MiddleStartVariant.Auto, "Auto per edge (fewest slivers, then most full boards)"),
            new ChoiceOption<MiddleStartVariant>(MiddleStartVariant.JointOnCentre, "D1 · Joint on centre line"),
            new ChoiceOption<MiddleStartVariant>(MiddleStartVariant.BoardCentred, "D2 · Board centred on centre line")
        };

        [ObservableProperty]
        private ChoiceOption<MiddleStartVariant> _selectedMiddleVariant;

        public NumericFieldViewModel MinPiece { get; } =
            new NumericFieldViewModel("Min cut piece", "m", 0.05, 1.0, Pattern2DSettings.MinBoardSizeM, "0.00");

        public bool IsRowStartEnabled => SelectedGridDirection.Value == GridDirectionMode.EachEdge;

        public bool IsMiddleVariantEnabled => IsRowStartEnabled && SelectedRowStart.Value == RowStartMode.FromMiddle;

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

        public bool HasErrors => Fields.Any(f => f.HasError) || (IsRowStartEnabled && MinPiece.HasError);

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
                GridOrigin = SelectedGridOrigin.Value,
                RowStart = SelectedRowStart.Value,
                MiddleVariant = SelectedMiddleVariant.Value,
                MinPieceM = MinPiece.Value ?? MinPiece.Default
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
            SelectedRowStart = RowStarts[0];
            SelectedMiddleVariant = MiddleVariants[0];
            MinPiece.Reset();
        }

        partial void OnSelectedGridDirectionChanged(ChoiceOption<GridDirectionMode> value)
        {
            OnPropertyChanged(nameof(IsEdgePickerEnabled));
            OnPropertyChanged(nameof(IsRowStartEnabled));
            OnPropertyChanged(nameof(IsMiddleVariantEnabled));
            RaiseChanged();
        }

        partial void OnSelectedEdgeChanged(OutlineEdgeInfo? value) => RaiseChanged();

        partial void OnSelectedGridOriginChanged(ChoiceOption<GridOriginMode> value) => RaiseChanged();

        partial void OnSelectedRowStartChanged(ChoiceOption<RowStartMode> value) => RaiseChanged();

        partial void OnSelectedMiddleVariantChanged(ChoiceOption<MiddleStartVariant> value) => RaiseChanged();

        private void RaiseChanged()
        {
            OnPropertyChanged(nameof(HasErrors));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
