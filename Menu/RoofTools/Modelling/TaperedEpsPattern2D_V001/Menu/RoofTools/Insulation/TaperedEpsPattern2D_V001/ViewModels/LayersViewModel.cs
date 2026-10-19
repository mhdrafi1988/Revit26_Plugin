using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using CommunityToolkit.Mvvm.ComponentModel;
using Revit26_Plugin.TaperedEpsPattern2D.V001.Core.Pattern;
using Revit26_Plugin.TaperedEpsPattern2D.V001.Revit;

namespace Revit26_Plugin.TaperedEpsPattern2D.V001.ViewModels
{
    /// <summary>Layers card: the four layers, contour interval, labels, grouping options.</summary>
    public sealed partial class LayersViewModel : ObservableObject
    {
        public LayersViewModel(IReadOnlyList<LineStyleOption> styles, IReadOnlyList<TextNoteTypeOption> textTypes)
        {
            Grid = new LayerOptionViewModel(PatternLayer.Grid, "Board grid", true, styles, LineStyleProvider.DefaultGrid);
            HipRidge = new LayerOptionViewModel(PatternLayer.HipRidge, "Hips / ridges", true, styles, LineStyleProvider.DefaultHipRidge);
            Contours = new LayerOptionViewModel(PatternLayer.Contour, "Thickness contours", true, styles, LineStyleProvider.DefaultContour);
            ContourInterval = new NumericFieldViewModel("Contour interval", "mm",
                SettingsValidator.MinContourIntervalMm, SettingsValidator.MaxContourIntervalMm, 10, "0");
            TextTypes = textTypes;
            _selectedTextType = textTypes.FirstOrDefault();
            _selectedLabelMode = LabelModes[0];

            foreach (LayerOptionViewModel layer in LineLayers)
            {
                layer.Changed += (_, _) => RaiseChanged();
            }

            ContourInterval.ValueChanged += (_, _) => RaiseChanged();
        }

        public LayerOptionViewModel Grid { get; }
        public LayerOptionViewModel HipRidge { get; }
        public LayerOptionViewModel Contours { get; }
        public IReadOnlyList<LayerOptionViewModel> LineLayers => new[] { Grid, HipRidge, Contours };

        public NumericFieldViewModel ContourInterval { get; }

        /// <summary>The fourth layer checkbox: board labels (default off = "None").</summary>
        [ObservableProperty]
        private bool _labelsEnabled;

        public IReadOnlyList<ChoiceOption<LabelMode>> LabelModes { get; } = new[]
        {
            new ChoiceOption<LabelMode>(LabelMode.Id, "Board ID"),
            new ChoiceOption<LabelMode>(LabelMode.Code, "Board code")
        };

        [ObservableProperty]
        private ChoiceOption<LabelMode> _selectedLabelMode;

        public IReadOnlyList<TextNoteTypeOption> TextTypes { get; }

        [ObservableProperty]
        private TextNoteTypeOption? _selectedTextType;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(CanReplace))]
        private bool _groupResult = true;

        [ObservableProperty]
        private bool _replacePrevious = true;

        /// <summary>Replace needs the group to find the previous pattern, so it is only available when grouping.</summary>
        public bool CanReplace => GroupResult;

        public LabelMode EffectiveLabelMode => LabelsEnabled ? SelectedLabelMode.Value : LabelMode.None;

        public bool AnyLayerEnabled => Grid.IsEnabled || HipRidge.IsEnabled || Contours.IsEnabled || LabelsEnabled;

        public bool HasErrors =>
            (Contours.IsEnabled && ContourInterval.HasError) ||
            (LabelsEnabled && SelectedTextType == null);

        public event EventHandler? Changed;

        public Pattern2DSettings ApplyTo(Pattern2DSettings settings) => settings with
        {
            DrawGrid = Grid.IsEnabled,
            DrawHipRidge = HipRidge.IsEnabled,
            DrawContours = Contours.IsEnabled,
            ContourIntervalMm = ContourInterval.Value ?? ContourInterval.Default,
            Labels = EffectiveLabelMode
        };

        public IReadOnlyDictionary<PatternLayer, ElementId> StyleIds() =>
            LineLayers.Where(l => l.SelectedStyle != null)
                      .ToDictionary(l => l.Layer, l => l.SelectedStyle!.GraphicsStyleId);

        public void Reset()
        {
            foreach (LayerOptionViewModel layer in LineLayers)
            {
                layer.Reset();
            }

            ContourInterval.Reset();
            LabelsEnabled = false;
            SelectedLabelMode = LabelModes[0];
            SelectedTextType = TextTypes.FirstOrDefault();
            GroupResult = true;
            ReplacePrevious = true;
        }

        partial void OnLabelsEnabledChanged(bool value) => RaiseChanged();

        partial void OnSelectedLabelModeChanged(ChoiceOption<LabelMode> value) => RaiseChanged();

        partial void OnSelectedTextTypeChanged(TextNoteTypeOption? value) => RaiseChanged();

        partial void OnGroupResultChanged(bool value) => RaiseChanged();

        partial void OnReplacePreviousChanged(bool value) => RaiseChanged();

        private void RaiseChanged()
        {
            OnPropertyChanged(nameof(AnyLayerEnabled));
            OnPropertyChanged(nameof(HasErrors));
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }
}
